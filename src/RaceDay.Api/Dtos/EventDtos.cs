using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>Fields used to create or update an event.</summary>
public class EventRequest
{
    /// <example>Pretoria Spring Marathon</example>
    [Required, StringLength(150, MinimumLength = 3)]
    public string EventName { get; set; } = string.Empty;

    /// <example>Annual road race through the Union Buildings precinct.</example>
    [StringLength(4000)]
    public string? Description { get; set; }

    /// <example>2026-11-15</example>
    [Required]
    public DateTime? EventDate { get; set; }

    /// <example>Pretoria, Gauteng</example>
    [Required, StringLength(200, MinimumLength = 2)]
    public string Location { get; set; } = string.Empty;

    /// <summary>Headline distance of the event in kilometres.</summary>
    /// <example>42.2</example>
    [Required, Range(0.1, 9999.99)]
    public decimal? DistanceKm { get; set; }

    /// <summary>Run, Walk or Cycle.</summary>
    /// <example>Run</example>
    [Required]
    public EventType? EventType { get; set; }
}

/// <summary>An event as returned by the API.</summary>
public class EventDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public decimal DistanceKm { get; set; }
    public EventType EventType { get; set; }
    public int OrganiserId { get; set; }
    public string OrganiserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }

    /// <summary>Requires Organiser to be loaded.</summary>
    public static EventDto From(Event e) => new()
    {
        EventId = e.EventId,
        EventName = e.EventName,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        OrganiserId = e.OrganiserId,
        OrganiserName = e.Organiser.FullName,
        CreatedAt = e.CreatedAt
    };
}
