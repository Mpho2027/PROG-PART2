using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Race results. Organisers capture finish times and positions; Participants view their own.</summary>
public class ResultsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public ResultsController(RaceDayDbContext db) => _db = db;

    /// <summary>Captures or updates the finishing result for an enrolment.</summary>
    /// <remarks>
    /// Only the Organiser of the enrolment's event may capture results, and only on or after the event date.
    /// Sending the request again for the same enrolment updates the existing result.
    /// Two participants in the same category cannot hold the same position.
    /// </remarks>
    /// <response code="201">Result created.</response>
    /// <response code="200">Existing result updated.</response>
    /// <response code="400">Validation failed (e.g. finishTime not hh:mm:ss) or the event has not taken place yet.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of the event.</response>
    /// <response code="404">The enrolment does not exist.</response>
    /// <response code="409">Another participant in this category already holds that position.</response>
    [HttpPost("api/enrolments/{id:int}/result")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Capture(int id, ResultRequest request)
    {
        var enrolment = await _db.EventEnrolments
            .Include(en => en.Participant)
            .Include(en => en.Category).ThenInclude(c => c.Event)
            .Include(en => en.Result)
            .SingleOrDefaultAsync(en => en.EnrolmentId == id);
        if (enrolment is null) return NotFoundMessage($"Enrolment {id} was not found.");
        if (enrolment.Category.Event.OrganiserId != CurrentUserId)
            return NotOwner("You can only capture results for events you organise.");
        if (enrolment.Category.Event.EventDate > Today)
            return BadRequestMessage("Results can only be captured on or after the event date.");

        var position = request.Position!.Value;
        var positionTaken = await _db.Results.AnyAsync(r =>
            r.Enrolment.CategoryId == enrolment.CategoryId && r.Position == position && r.EnrolmentId != id);
        if (positionTaken)
            return ConflictMessage($"Position {position} is already assigned to another participant in this category.");

        var isNew = enrolment.Result is null;
        var result = enrolment.Result ?? new Result { EnrolmentId = id, Enrolment = enrolment };
        result.FinishTime = TimeSpan.ParseExact(request.FinishTime, new[] { @"hh\:mm\:ss", @"h\:mm\:ss" }, CultureInfo.InvariantCulture);
        result.Position = position;
        result.Status = "Final";

        if (isNew) _db.Results.Add(result);
        await _db.SaveChangesAsync();

        var dto = ResultDto.From(result);
        return isNew ? Created($"/api/categories/{enrolment.CategoryId}/results", dto) : Ok(dto);
    }

    /// <summary>Returns the leaderboard for a category, ordered by position.</summary>
    /// <response code="200">Ordered array of results (unplaced entries last).</response>
    /// <response code="404">The category does not exist.</response>
    [HttpGet("api/categories/{categoryId:int}/results")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForCategory(int categoryId)
    {
        if (!await _db.Categories.AnyAsync(c => c.CategoryId == categoryId))
            return NotFoundMessage($"Category {categoryId} was not found.");

        var results = await LoadResults()
            .Where(r => r.Enrolment.CategoryId == categoryId)
            .OrderBy(r => r.Position == null).ThenBy(r => r.Position).ThenBy(r => r.FinishTime)
            .ToListAsync();
        return Ok(results.Select(ResultDto.From));
    }

    /// <summary>Returns the logged-in Participant's own results across all events.</summary>
    /// <response code="200">Array of the Participant's results, most recent event first.</response>
    /// <response code="403">Only Participants have results.</response>
    [HttpGet("api/users/me/results")]
    [Authorize(Roles = AppRoles.Participant)]
    [ProducesResponseType(typeof(IEnumerable<ResultDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var participantId = CurrentUserId;
        var results = await LoadResults()
            .Where(r => r.Enrolment.ParticipantId == participantId)
            .OrderByDescending(r => r.Enrolment.Category.Event.EventDate)
            .ToListAsync();
        return Ok(results.Select(ResultDto.From));
    }

    private IQueryable<Result> LoadResults() =>
        _db.Results
            .Include(r => r.Enrolment).ThenInclude(en => en.Participant)
            .Include(r => r.Enrolment).ThenInclude(en => en.Category).ThenInclude(c => c.Event)
            .AsNoTracking();
}
