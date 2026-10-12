namespace RaceDay.Api.Models;

/// <summary>
/// Links a Participant to an event through the category they selected.
/// The event is reached through Category.EventId. Maps to dbo.EventEnrolments.
/// </summary>
public class EventEnrolment
{
    public int EnrolmentId { get; set; }
    public int ParticipantId { get; set; }
    public int CategoryId { get; set; }
    public string? BibNumber { get; set; }
    public DateTime EnrolmentDate { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "Confirmed";

    public User Participant { get; set; } = null!;
    public Category Category { get; set; } = null!;
    public Result? Result { get; set; }
}
