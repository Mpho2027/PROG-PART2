using System.ComponentModel.DataAnnotations;
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

    /// <summary>True once the Organiser has captured a result for this enrolment.</summary>
    public bool HasResult { get; set; }

    /// <summary>Captured finish time (hh:mm:ss), if any.</summary>
    public string? FinishTime { get; set; }

    /// <summary>Captured finishing position, if any.</summary>
    public int? Position { get; set; }

    /// <summary>Requires Participant, Category.Event and Result to be loaded.</summary>
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
        EventDate = en.Category.Event.EventDate,
        HasResult = en.Result is not null,
        FinishTime = en.Result?.FinishTime?.ToString(@"hh\:mm\:ss"),
        Position = en.Result?.Position
    };
}

/// <summary>New status for an enrolment, set by the event's Organiser.</summary>
public class EnrolmentStatusRequest
{
    /// <summary>Pending, Confirmed or Cancelled.</summary>
    /// <example>Confirmed</example>
    [Required]
    public string Status { get; set; } = string.Empty;
}
