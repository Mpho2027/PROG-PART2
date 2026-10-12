namespace RaceDay.Api.Models;

/// <summary>The finishing result for one enrolment (one-to-one). Maps to dbo.Results.</summary>
public class Result
{
    public int ResultId { get; set; }
    public int EnrolmentId { get; set; }
    public TimeSpan? FinishTime { get; set; }
    public int? Position { get; set; }
    public string Status { get; set; } = "Pending";

    public EventEnrolment Enrolment { get; set; } = null!;
}
