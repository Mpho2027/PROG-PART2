using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>
/// Event enrolments. A Participant enters an event by selecting one of its categories;
/// the enrolment records the Participant, the category and (through it) the event.
/// </summary>
public class EnrolmentsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public EnrolmentsController(RaceDayDbContext db) => _db = db;

    /// <summary>Enrols the logged-in Participant into a category.</summary>
    /// <remarks>
    /// No request body is needed: the Participant is taken from the session. A bib number is assigned automatically.
    /// A Participant may enter each category only once, and only while places remain and the event has not yet taken place.
    /// </remarks>
    /// <response code="201">Enrolled. Returns the enrolment with its bib number.</response>
    /// <response code="400">The event has already taken place.</response>
    /// <response code="403">Only Participants can enrol.</response>
    /// <response code="404">The category does not exist.</response>
    /// <response code="409">Already enrolled in this category, or the category is full.</response>
    [HttpPost("api/categories/{categoryId:int}/enrol")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(EnrolmentDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Enrol(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundMessage($"Category {categoryId} was not found.");
        if (category.Event.EventDate < Today) return BadRequestMessage("This event has already taken place.");

        var participantId = CurrentUserId;
        if (await _db.EventEnrolments.AnyAsync(en => en.CategoryId == categoryId && en.ParticipantId == participantId))
            return ConflictMessage("You are already enrolled in this category.");

        var enrolled = await _db.EventEnrolments.CountAsync(en => en.CategoryId == categoryId);
        if (enrolled >= category.MaxParticipants)
            return ConflictMessage("This category is full.");

        var enrolment = new EventEnrolment
        {
            ParticipantId = participantId,
            CategoryId = categoryId,
            EnrolmentDate = DateTime.UtcNow,
            Status = "Confirmed"
        };
        _db.EventEnrolments.Add(enrolment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException)
        {
            // UQ_Enrolment_Participant_Category caught a simultaneous duplicate request.
            return ConflictMessage("You are already enrolled in this category.");
        }

        // The bib number is derived from the generated EnrolmentID so it is always unique.
        enrolment.BibNumber = $"RD{enrolment.EnrolmentId:D5}";
        await _db.SaveChangesAsync();

        var created = await LoadEnrolments().SingleAsync(en => en.EnrolmentId == enrolment.EnrolmentId);
        return Created($"/api/enrolments/{enrolment.EnrolmentId}", EnrolmentDto.From(created));
    }

    /// <summary>Lists all enrolments belonging to the logged-in Participant.</summary>
    /// <response code="200">Array of the Participant's enrolments, soonest event first.</response>
    /// <response code="403">Only Participants have enrolments.</response>
    [HttpGet("api/users/me/enrolments")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;
        var enrolments = await LoadEnrolments()
            .Where(en => en.ParticipantId == participantId)
            .OrderBy(en => en.Category.Event.EventDate)
            .ToListAsync();
        return Ok(enrolments.Select(EnrolmentDto.From));
    }

    /// <summary>Lists all participants enrolled in a category.</summary>
    /// <remarks>Only the Organiser of the category's event may view its enrolments.</remarks>
    /// <response code="200">Array of enrolments in the category.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpGet("api/categories/{categoryId:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForCategory(int categoryId)
    {
        var category = await _db.Categories.Include(c => c.Event).AsNoTracking().SingleOrDefaultAsync(c => c.CategoryId == categoryId);
        if (category is null) return NotFoundMessage($"Category {categoryId} was not found.");
        if (category.Event.OrganiserId != CurrentUserId) return NotOwner("You can only view enrolments for events you organise.");

        var enrolments = await LoadEnrolments()
            .Where(en => en.CategoryId == categoryId)
            .OrderBy(en => en.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(EnrolmentDto.From));
    }

    /// <summary>Lists all enrolments across every category of an event.</summary>
    /// <remarks>Only the event's Organiser may view its enrolments.</remarks>
    /// <response code="200">Array of enrolments for the event, grouped by category.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of the event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/enrolments")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<EnrolmentDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        var ev = await _db.Events.AsNoTracking().SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundMessage($"Event {eventId} was not found.");
        if (ev.OrganiserId != CurrentUserId) return NotOwner("You can only view enrolments for events you organise.");

        var enrolments = await LoadEnrolments()
            .Where(en => en.Category.EventId == eventId)
            .OrderBy(en => en.Category.CategoryName).ThenBy(en => en.EnrolmentDate)
            .ToListAsync();
        return Ok(enrolments.Select(EnrolmentDto.From));
    }

    /// <summary>Cancels an enrolment (withdrawal).</summary>
    /// <remarks>
    /// A Participant may withdraw only their own enrolment; an Admin may cancel any.
    /// An enrolment that already has a result cannot be withdrawn.
    /// </remarks>
    /// <response code="204">Enrolment cancelled.</response>
    /// <response code="403">Not the Participant who owns this enrolment (and not an Admin).</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">A result has already been captured for this enrolment.</response>
    [HttpDelete("api/enrolments/{id:int}")]
    [Authorize(Roles = AppRoles.Participant + "," + AppRoles.Admin)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Withdraw(int id)
    {
        var enrolment = await _db.EventEnrolments.Include(en => en.Result).SingleOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null) return NotFoundMessage($"Enrolment {id} was not found.");
        if (!IsInRole(AppRoles.Admin) && enrolment.ParticipantId != CurrentUserId)
            return NotOwner("You can only withdraw your own enrolments.");
        if (enrolment.Result is not null)
            return ConflictMessage("This enrolment already has a result and cannot be withdrawn.");

        _db.EventEnrolments.Remove(enrolment);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private IQueryable<EventEnrolment> LoadEnrolments() =>
        _db.EventEnrolments
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c.Event)
            .AsNoTracking();
}
