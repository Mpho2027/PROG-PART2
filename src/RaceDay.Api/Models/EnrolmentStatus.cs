namespace RaceDay.Api.Models;

/// <summary>Allowed values for EventEnrolments.Status.</summary>
public static class EnrolmentStatus
{
    public const string Pending = "Pending";
    public const string Confirmed = "Confirmed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Confirmed, Cancelled };

    /// <summary>Returns the correctly-cased status, or null when the value is not an allowed status.</summary>
    public static string? Normalise(string? value) =>
        All.FirstOrDefault(s => string.Equals(s, value?.Trim(), StringComparison.OrdinalIgnoreCase));
}
