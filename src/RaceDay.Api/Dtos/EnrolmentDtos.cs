using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>An enrolment: the link between a Participant, an event and the category they selected.</summary>
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

    /// <summary>Requires Participant and Category.Event to be loaded.</summary>
    public static EnrolmentDto From(EventEnrolment en) => new()
    {
        EnrolmentId = en.EnrolmentId,
        BibNumber = en.BibNumber,
        Status = en.Status,
        EnrolmentDate = en.EnrolmentDate,
        ParticipantId = en.ParticipantId,
        ParticipantName = en.Participant.FullName,
        ParticipantEmail = en.Participant.Email,
        CategoryId = en.CategoryId,
        CategoryName = en.Category.CategoryName,
        EventId = en.Category.EventId,
        EventName = en.Category.Event.EventName,
        EventDate = en.Category.Event.EventDate
    };
}
