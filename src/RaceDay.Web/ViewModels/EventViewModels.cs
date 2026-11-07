using System.ComponentModel.DataAnnotations;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.ViewModels;

/// <summary>Filter options on the events home page (sent to the API as query parameters).</summary>
public class EventFilterViewModel
{
    [StringLength(100)]
    [Display(Name = "Search")]
    public string? Search { get; set; }

    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "From")]
    public DateTime? From { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "To")]
    public DateTime? To { get; set; }

    public bool HasFilters => !string.IsNullOrWhiteSpace(Search) || EventType.HasValue || From.HasValue || To.HasValue;
}

public class EventsIndexViewModel
{
    public EventFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<EventDto> Events { get; set; } = Array.Empty<EventDto>();
    public string? ErrorMessage { get; set; }
}

public class EventDetailsViewModel
{
    public EventDto Event { get; set; } = new();
    public IReadOnlyList<CategoryDto> Categories { get; set; } = Array.Empty<CategoryDto>();

    /// <summary>The logged-in participant's enrolments in this event, by category.</summary>
    public IReadOnlyDictionary<int, EnrolmentDto> MyEnrolments { get; set; } = new Dictionary<int, EnrolmentDto>();

    /// <summary>Published results per category (shown once the event has taken place).</summary>
    public IReadOnlyDictionary<int, List<ResultDto>> Results { get; set; } = new Dictionary<int, List<ResultDto>>();

    public bool IsOwner { get; set; }
    public int TotalEntries => Categories.Sum(c => c.EnrolledCount);
}
