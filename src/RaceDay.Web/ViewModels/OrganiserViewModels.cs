using System.ComponentModel.DataAnnotations;
using System.Globalization;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.ViewModels;

/// <summary>Create / edit event form. Field names match the API's EventRequest so its errors land on the right input.</summary>
public class EventFormViewModel
{
    public int? EventId { get; set; }

    [Required(ErrorMessage = "Enter the event name.")]
    [StringLength(150, MinimumLength = 3, ErrorMessage = "The event name must be between 3 and 150 characters.")]
    [Display(Name = "Event name")]
    public string EventName { get; set; } = string.Empty;

    [StringLength(4000, ErrorMessage = "The description can be at most 4000 characters.")]
    [DataType(DataType.MultilineText)]
    public string? Description { get; set; }

    [Required(ErrorMessage = "Choose the event date.")]
    [DataType(DataType.Date)]
    [Display(Name = "Event date")]
    public DateTime? EventDate { get; set; }

    [Required(ErrorMessage = "Enter where the event takes place.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "The location must be between 2 and 200 characters.")]
    public string Location { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the distance.")]
    [Range(0.1, 9999.99, ErrorMessage = "The distance must be between 0.1 and 9999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Required(ErrorMessage = "Choose the event type.")]
    [Display(Name = "Event type")]
    public EventType? EventType { get; set; }

    [Display(Name = "Banner image")]
    public IFormFile? BannerImage { get; set; }

    public bool HasBanner { get; set; }

    /// <summary>The body sent to the API.</summary>
    public object ToRequest() => new
    {
        eventName = EventName.Trim(),
        description = string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
        eventDate = EventDate!.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        location = Location.Trim(),
        distanceKm = DistanceKm,
        eventType = EventType.ToString()
    };

    public static EventFormViewModel From(EventDto e) => new()
    {
        EventId = e.EventId,
        EventName = e.EventName,
        Description = e.Description,
        EventDate = e.EventDate,
        Location = e.Location,
        DistanceKm = e.DistanceKm,
        EventType = e.EventType,
        HasBanner = e.HasBanner
    };
}

/// <summary>Add / edit category form. Use a distance (e.g. 10km), an age range (e.g. Under 20) or both.</summary>
public class CategoryFormViewModel : IValidatableObject
{
    public int EventId { get; set; }
    public int? CategoryId { get; set; }
    public string EventName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter the category name.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "The category name must be between 2 and 100 characters.")]
    [Display(Name = "Category name")]
    public string CategoryName { get; set; } = string.Empty;

    [Range(0.1, 999.99, ErrorMessage = "The distance must be between 0.1 and 999.99 km.")]
    [Display(Name = "Distance (km)")]
    public decimal? DistanceKm { get; set; }

    [Range(0, 120, ErrorMessage = "Minimum age must be between 0 and 120.")]
    [Display(Name = "Minimum age")]
    public int? MinAge { get; set; }

    [Range(0, 120, ErrorMessage = "Maximum age must be between 0 and 120.")]
    [Display(Name = "Maximum age")]
    public int? MaxAge { get; set; }

    [Required(ErrorMessage = "Enter how many people can enter.")]
    [Range(1, 100000, ErrorMessage = "Places must be between 1 and 100 000.")]
    [Display(Name = "Places available")]
    public int? MaxParticipants { get; set; } = 100;

    [Required(ErrorMessage = "Enter the entry fee (0 for free).")]
    [Range(0, 999999.99, ErrorMessage = "The entry fee cannot be negative.")]
    [Display(Name = "Entry fee (R)")]
    public decimal? EntryFee { get; set; } = 0;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinAge.HasValue && MaxAge.HasValue && MinAge > MaxAge)
            yield return new ValidationResult("Minimum age cannot be greater than maximum age.", new[] { nameof(MinAge) });
    }

    public object ToRequest() => new
    {
        categoryName = CategoryName.Trim(),
        distanceKm = DistanceKm,
        minAge = MinAge,
        maxAge = MaxAge,
        maxParticipants = MaxParticipants,
        entryFee = EntryFee
    };

    public static CategoryFormViewModel From(CategoryDto c) => new()
    {
        EventId = c.EventId,
        CategoryId = c.CategoryId,
        EventName = c.EventName,
        CategoryName = c.CategoryName,
        DistanceKm = c.DistanceKm,
        MinAge = c.MinAge,
        MaxAge = c.MaxAge,
        MaxParticipants = c.MaxParticipants,
        EntryFee = c.EntryFee
    };
}

public class CategoriesPageViewModel
{
    public EventDto Event { get; set; } = new();
    public IReadOnlyList<CategoryDto> Categories { get; set; } = Array.Empty<CategoryDto>();
}
