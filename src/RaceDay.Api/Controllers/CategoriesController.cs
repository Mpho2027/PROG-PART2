using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Age or distance categories within an event (e.g. Under 20, Senior, 10km, 21km).</summary>
public class CategoriesController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public CategoriesController(RaceDayDbContext db) => _db = db;

    /// <summary>Adds a race category (age or distance) to an event.</summary>
    /// <remarks>
    /// Only the event's Organiser may add categories. For a distance category set distanceKm;
    /// for an age category set minAge and/or maxAge.
    /// </remarks>
    /// <response code="201">Category created. Returns the new category.</response>
    /// <response code="400">Validation failed, e.g. minAge is greater than maxAge.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of this event.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpPost("api/events/{eventId:int}/categories")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(int eventId, CategoryRequest request)
    {
        var ev = await _db.Events.SingleOrDefaultAsync(e => e.EventId == eventId);
        if (ev is null) return NotFoundMessage($"Event {eventId} was not found.");
        if (ev.OrganiserId != CurrentUserId) return NotOwner("You can only add categories to events you organise.");

        var category = new Category { EventId = eventId, Event = ev };
        Apply(category, request);
        _db.Categories.Add(category);
        await _db.SaveChangesAsync();

        return Created($"/api/events/{eventId}/categories", CategoryDto.From(category));
    }

    /// <summary>Lists all categories for a specific event.</summary>
    /// <remarks>Each category includes how many participants are enrolled and how many places remain.</remarks>
    /// <response code="200">Array of categories.</response>
    /// <response code="404">The event does not exist.</response>
    [HttpGet("api/events/{eventId:int}/categories")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<CategoryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetForEvent(int eventId)
    {
        if (!await _db.Events.AnyAsync(e => e.EventId == eventId))
            return NotFoundMessage($"Event {eventId} was not found.");

        var categories = await _db.Categories
            .Include(c => c.Event).Include(c => c.Enrolments)
            .AsNoTracking()
            .Where(c => c.EventId == eventId)
            .OrderBy(c => c.DistanceKm).ThenBy(c => c.MinAge).ThenBy(c => c.CategoryName)
            .ToListAsync();
        return Ok(categories.Select(CategoryDto.From));
    }

    /// <summary>Updates a category's details.</summary>
    /// <remarks>Only the event's Organiser may update it. maxParticipants cannot drop below the number already enrolled.</remarks>
    /// <response code="200">Category updated. Returns the updated category.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpPut("api/categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(CategoryDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, CategoryRequest request)
    {
        var category = await _db.Categories.Include(c => c.Event).Include(c => c.Enrolments)
            .SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundMessage($"Category {id} was not found.");
        if (category.Event.OrganiserId != CurrentUserId) return NotOwner("You can only update categories of events you organise.");
        if (request.MaxParticipants < category.Enrolments.Count)
            return BadRequestMessage($"maxParticipants cannot be lower than the {category.Enrolments.Count} participants already enrolled.");

        Apply(category, request);
        await _db.SaveChangesAsync();
        return Ok(CategoryDto.From(category));
    }

    /// <summary>Removes a category from an event.</summary>
    /// <remarks>Only the event's Organiser may delete it. Enrolments and results in the category are removed too.</remarks>
    /// <response code="204">Category deleted.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of the event.</response>
    /// <response code="404">The category does not exist.</response>
    [HttpDelete("api/categories/{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var category = await _db.Categories.Include(c => c.Event)
            .Include(c => c.Enrolments).ThenInclude(en => en.Result)
            .SingleOrDefaultAsync(c => c.CategoryId == id);
        if (category is null) return NotFoundMessage($"Category {id} was not found.");
        if (category.Event.OrganiserId != CurrentUserId) return NotOwner("You can only delete categories of events you organise.");

        _db.Categories.Remove(category);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Category category, CategoryRequest request)
    {
        category.CategoryName = request.CategoryName.Trim();
        category.DistanceKm = request.DistanceKm;
        category.MinAge = request.MinAge;
        category.MaxAge = request.MaxAge;
        category.MaxParticipants = request.MaxParticipants;
        category.EntryFee = request.EntryFee;
    }
}
