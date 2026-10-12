using System.ComponentModel.DataAnnotations;
using RaceDay.Api.Models;

namespace RaceDay.Api.Dtos;

/// <summary>The finish time and position captured for a Participant.</summary>
public class ResultRequest
{
    /// <summary>Finish time in hh:mm:ss format.</summary>
    /// <example>00:52:14</example>
    [Required, RegularExpression(@"^([0-9]|[01][0-9]|2[0-3]):[0-5][0-9]:[0-5][0-9]$",
        ErrorMessage = "finishTime must be in hh:mm:ss format, e.g. 00:52:14.")]
    public string FinishTime { get; set; } = string.Empty;

    /// <summary>Finishing position within the category (1 = winner).</summary>
    /// <example>12</example>
    [Required, Range(1, 100000)]
    public int? Position { get; set; }
}

/// <summary>A result as returned by the API.</summary>
public class ResultDto
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }

    /// <example>00:52:14</example>
    public string? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ParticipantId { get; set; }
    public string ParticipantName { get; set; } = string.Empty;
    public string? BibNumber { get; set; }
    public int CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public int EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public DateTime EventDate { get; set; }

    /// <summary>Requires Enrolment.Participant and Enrolment.Category.Event to be loaded.</summary>
    public static ResultDto From(Result r) => new()
    {
        ResultId = r.ResultId,
        EnrolmentId = r.EnrolmentId,
        FinishTime = r.FinishTime?.ToString(@"hh\:mm\:ss"),
        Position = r.Position,
        Status = r.Status,
        ParticipantId = r.Enrolment.ParticipantId,
        ParticipantName = r.Enrolment.Participant.FullName,
        BibNumber = r.Enrolment.BibNumber,
        CategoryId = r.Enrolment.CategoryId,
        CategoryName = r.Enrolment.Category.CategoryName,
        EventId = r.Enrolment.Category.EventId,
        EventName = r.Enrolment.Category.Event.EventName,
        EventDate = r.Enrolment.Category.Event.EventDate
    };
}
