namespace RaceDay.Web.Models.Api;

// Shapes of the JSON returned by the RaceDay API (Part 2). The MVC app only reads and sends these;
// all rules about events, enrolments and results stay in the API.

public enum EventType
{
    Run,
    Walk,
    Cycle
}

public static class Roles
{
    public const string Organiser = "Organiser";
    public const string Participant = "Participant";
}

public static class EnrolmentStatuses
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = { Pending, Confirmed, Cancelled };
}

public class UserProfileDto
{
    public int UserId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasProfilePicture { get; set; }
}

public class LoginResponse
{
    public string Message { get; set; } = string.Empty;
    public UserProfileDto User { get; set; } = new();
}

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
    public bool HasBanner { get; set; }

    public bool IsPast => EventDate.Date < DateTime.Today;
}

public class OrganiserEventSummaryDto
{
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public EventType EventType { get; set; }
    public decimal DistanceKm { get; set; }
    public int CategoryCount { get; set; }
    public int EnrolmentCount { get; set; }
    public int Capacity { get; set; }
    public int ResultsCaptured { get; set; }
    public bool IsUpcoming { get; set; }
    public bool HasBanner { get; set; }
}

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
}

public class EnrolmentDto
{
    public int EnrolmentId { get; set; }
    public string? BibNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime EnrolmentDate { get; set; }
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string ParticipantEmail { get; set; } = string.Empty;
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
    public bool HasResult { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
}

public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public int Finishers { get; set; }
    public string Status { get; set; } = string.Empty;
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }
}

/// <summary>Filters for the public event list, passed straight through to GET /api/events.</summary>
public class EventFilter
{
    public string? Search { get; set; }
    public EventType? EventType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public bool IncludePast { get; set; }
}

/// <summary>An image streamed from the API (which reads it from Azure Blob Storage).</summary>
public sealed record ApiImage(byte[] Content, string ContentType);
