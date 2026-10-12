using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>
/// Fields used to create or update a category. A category can be distance-based
/// (set distanceKm, e.g. "10km"), age-based (set minAge and/or maxAge, e.g. "Under 20"), or both.
/// </summary>
public class CategoryRequest : IValidatableObject
{
    /// <example>10km Fun Run</example>
    [Required, StringLength(100, MinimumLength = 2)]
    public string CategoryName { get; set; } = string.Empty;

    /// <example>10</example>
    [Range(0.1, 999.99)]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120)]
    public int? MinAge { get; set; }

    [Range(0, 120)]
    public int? MaxAge { get; set; }

    /// <example>500</example>
    [Range(1, 100000)]
    public int MaxParticipants { get; set; } = 100;

    /// <summary>Entry fee in Rand.</summary>
    /// <example>150</example>
    [Range(0, 999999.99)]
    public decimal EntryFee { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
            yield return new ValidationResult("minAge cannot be greater than maxAge.", new[] { nameof(MinAge), nameof(MaxAge) });
    }
}

/// <summary>A category as returned by the API, with live enrolment counts.</summary>
public class CategoryDto
{
    public int CategoryId { get; set; }
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public decimal? DistanceKm { get; set; }
    public int? MinAge { get; set; }
    public int? MaxAge { get; set; }
    public int MaxParticipants { get; set; }
    public decimal EntryFee { get; set; }
    public int EnrolledCount { get; set; }
    public int SpotsRemaining { get; set; }

    /// <summary>Requires Event and Enrolments to be loaded.</summary>
    public static CategoryDto From(Category c) => new()
    {
        CategoryId = c.CategoryId,
        EventId = c.EventId,
        EventName = c.Event.EventName,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        MaxParticipants = c.MaxParticipants,
        EntryFee = c.EntryFee,
        EnrolledCount = c.Enrolments.Count,
        SpotsRemaining = Math.Max(0, c.MaxParticipants - c.Enrolments.Count)
    };
}
