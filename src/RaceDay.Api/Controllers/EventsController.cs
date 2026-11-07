using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RaceDay.Api.Auth;
using RaceDay.Api.Data;
using RaceDay.Api.Dtos;
using RaceDay.Api.Models;

namespace RaceDay.Api.Controllers;

/// <summary>Race events. Anyone can view events; Organisers create and manage their own.</summary>
[Route("api/events")]
public class EventsController : RaceDayControllerBase
{
    private readonly RaceDayDbContext _db;

    public EventsController(RaceDayDbContext db) => _db = db;

    /// <summary>Creates a new event owned by the logged-in Organiser.</summary>
    /// <response code="201">Event created. Returns the new event.</response>
    /// <response code="400">Validation failed, e.g. a missing name or an event type that is not Run, Walk or Cycle.</response>
    [HttpPost]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(EventRequest request)
    {
        var ev = new Event { OrganiserId = CurrentUserId, CreatedAt = DateTime.UtcNow };
        Apply(ev, request);

        _db.Events.Add(ev);
        await _db.SaveChangesAsync();
        await _db.Entry(ev).Reference(e => e.Organiser).LoadAsync();

        return CreatedAtAction(nameof(GetById), new { id = ev.EventId }, EventDto.From(ev));
    }

    /// <summary>Lists all upcoming events, with optional filters.</summary>
    /// <remarks>
    /// Events are ordered by date. Set includePast=true to include events that have already taken place.
    /// Filter by eventType (Run, Walk, Cycle), by search text (matched against name, location and description)
    /// and by a date range (from / to).
    /// </remarks>
    /// <response code="200">Array of events.</response>
    /// <response code="400">A filter value is invalid, e.g. from is after to.</response>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(IEnumerable<EventDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAll([FromQuery] EventQuery filter)
    {
        if (filter.From.HasValue && filter.To.HasValue && filter.From.Value.Date > filter.To.Value.Date)
            return BadRequestMessage("The 'from' date cannot be after the 'to' date.");

        var query = _db.Events.Include(e => e.Organiser).AsNoTracking();
        if (!filter.IncludePast)
            query = query.Where(e => e.EventDate >= Today);
        if (filter.EventType.HasValue)
            query = query.Where(e => e.EventType == filter.EventType.Value);
        if (filter.From.HasValue)
            query = query.Where(e => e.EventDate >= filter.From.Value.Date);
        if (filter.To.HasValue)
            query = query.Where(e => e.EventDate <= filter.To.Value.Date);
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            // Lower-casing both sides keeps the search case-insensitive on every database provider.
            var text = filter.Search.Trim().ToLower();
            query = query.Where(e => e.EventName.ToLower().Contains(text) || e.Location.ToLower().Contains(text)
                                     || (e.Description != null && e.Description.ToLower().Contains(text)));
        }

        var events = await query.OrderBy(e => e.EventDate).ThenBy(e => e.EventName).ToListAsync();
        return Ok(events.Select(EventDto.From));
    }

    /// <summary>Lists the logged-in Organiser's own events with enrolment totals (dashboard summary).</summary>
    /// <remarks>Upcoming events come first (soonest first), followed by past events (most recent first).</remarks>
    /// <response code="200">Array of the Organiser's events with category, enrolment and result counts.</response>
    /// <response code="403">Only Organisers have events to manage.</response>
    [HttpGet("~/api/users/me/events")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(IEnumerable<OrganiserEventSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMine()
    {
        var organiserId = CurrentUserId;
        var today = Today;
        var events = await _db.Events
            .AsNoTracking()
            .Where(e => e.OrganiserId == organiserId)
            .Select(e => new OrganiserEventSummaryDto
            {
                EventId = e.EventId,
                EventName = e.EventName,
                EventDate = e.EventDate,
                Location = e.Location,
                EventType = e.EventType,
                DistanceKm = e.DistanceKm,
                CategoryCount = e.Categories.Count,
                EnrolmentCount = e.Categories.SelectMany(c => c.Enrolments).Count(en => en.Status != EnrolmentStatus.Cancelled),
                Capacity = e.Categories.Sum(c => c.MaxParticipants),
                ResultsCaptured = e.Categories.SelectMany(c => c.Enrolments).Count(en => en.Result != null),
                IsUpcoming = e.EventDate >= today
            })
            .ToListAsync();

        var ordered = events.Where(e => e.IsUpcoming).OrderBy(e => e.EventDate)
            .Concat(events.Where(e => !e.IsUpcoming).OrderByDescending(e => e.EventDate));
        return Ok(ordered);
    }

    /// <summary>Retrieves the details of a single event.</summary>
    /// <response code="200">The event.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpGet("{id:int}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(int id)
    {
        var ev = await _db.Events.Include(e => e.Organiser).AsNoTracking().SingleOrDefaultAsync(e => e.EventId == id);
        return ev is null ? NotFoundMessage($"Event {id} was not found.") : Ok(EventDto.From(ev));
    }

    /// <summary>Updates an event's details.</summary>
    /// <remarks>Only the Organiser who created the event may update it.</remarks>
    /// <response code="200">Event updated. Returns the updated event.</response>
    /// <response code="400">Validation failed.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of this event.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpPut("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(typeof(EventDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(int id, EventRequest request)
    {
        var ev = await _db.Events.Include(e => e.Organiser).SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundMessage($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return NotOwner("You can only update events you organise.");

        Apply(ev, request);
        await _db.SaveChangesAsync();
        return Ok(EventDto.From(ev));
    }

    /// <summary>Deletes an event and its categories.</summary>
    /// <remarks>Only the event's Organiser may delete it. Its categories, enrolments and results are removed too.</remarks>
    /// <response code="204">Event deleted.</response>
    /// <response code="403">Not a logged-in Organiser, or not the owner of this event.</response>
    /// <response code="404">No event with this ID.</response>
    [HttpDelete("{id:int}")]
    [Authorize(Roles = AppRoles.Organiser)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(MessageResponse), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(int id)
    {
        var ev = await _db.Events
            .Include(e => e.Categories).ThenInclude(c => c.Enrolments).ThenInclude(en => en.Result)
            .SingleOrDefaultAsync(e => e.EventId == id);
        if (ev is null) return NotFoundMessage($"Event {id} was not found.");
        if (ev.OrganiserId != CurrentUserId) return NotOwner("You can only delete events you organise.");

        _db.Events.Remove(ev);
        await _db.SaveChangesAsync();
        return NoContent();
    }

    private static void Apply(Event ev, EventRequest request)
    {
        ev.EventName = request.EventName.Trim();
        ev.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        ev.EventDate = request.EventDate!.Value.Date;
        ev.Location = request.Location.Trim();
        ev.DistanceKm = request.DistanceKm!.Value;
        ev.EventType = request.EventType!.Value;
    }
}
