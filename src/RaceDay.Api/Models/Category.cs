namespace RaceDay.Api.Models;

/// <summary>
/// An age or distance category within an event (e.g. "Under 20", "Senior", "10km").
/// Maps to dbo.Categories.
/// </summary>
public class Category
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string CategoryName { get; set; } = string.Empty;

    /// <summary>Distance for distance-based categories; null for purely age-based ones.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>Lower age bound (inclusive) for age-based categories.</summary>
    public int? MinAge { get; set; }

    /// <summary>Upper age bound (inclusive) for age-based categories.</summary>
    public int? MaxAge { get; set; }

    public int MaxParticipants { get; set; } = 100;
    public decimal EntryFee { get; set; }

    public Event Event { get; set; } = null!;
    public ICollection<EventEnrolment> Enrolments { get; set; } = new List<EventEnrolment>();
}
