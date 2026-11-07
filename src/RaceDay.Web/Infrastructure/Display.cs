using System.Globalization;
using RaceDay.Web.Models.Api;

namespace RaceDay.Web.Infrastructure;

/// <summary>Formatting helpers used by the views so numbers, dates and labels look the same on every page.</summary>
public static class Display
{
    private static readonly CultureInfo SouthAfrica = CultureInfo.GetCultureInfo("en-ZA");

    /// <summary>1 becomes "1st", 22 becomes "22nd", 113 becomes "113th".</summary>
    public static string Ordinal(int number)
    {
        var lastTwo = number % 100;
        var suffix = lastTwo is >= 11 and <= 13 ? "th" : (number % 10) switch
        {
            1 => "st",
            2 => "nd",
            3 => "rd",
            _ => "th"
        };
        return $"{number}{suffix}";
    }

    /// <summary>"47th of 312 finishers".</summary>
    public static string Placing(int position, int finishers) =>
        finishers > 0 ? $"{Ordinal(position)} of {finishers} finisher{(finishers == 1 ? "" : "s")}" : Ordinal(position);

    public static string TypeIcon(EventType type) => type switch
    {
        EventType.Run => "directions_run",
        EventType.Walk => "directions_walk",
        EventType.Cycle => "directions_bike",
        _ => "event"
    };

    public static string TypeLabel(EventType type) => type switch
    {
        EventType.Run => "Running",
        EventType.Walk => "Walking",
        EventType.Cycle => "Cycling",
        _ => type.ToString()
    };

    public static string Km(decimal km) => $"{km.ToString("0.##", CultureInfo.InvariantCulture)} km";

    public static string Rand(decimal amount) => amount == 0 ? "Free" : "R" + amount.ToString("#,0.##", SouthAfrica);

    public static string Date(DateTime date) => date.ToString("ddd, d MMMM yyyy", SouthAfrica);

    public static string ShortDate(DateTime date) => date.ToString("d MMM yyyy", SouthAfrica);

    public static string Month(DateTime date) => date.ToString("MMM", SouthAfrica);

    /// <summary>"Under 20", "20 to 39", "40+" for age categories; empty when the category has no age limits.</summary>
    public static string AgeRange(int? minAge, int? maxAge) => (minAge, maxAge) switch
    {
        (null, null) => string.Empty,
        (null, { } max) => $"Under {max + 1}",
        ({ } min, null) => $"{min}+",
        ({ } min, { } max) => $"Ages {min} to {max}"
    };

    public static string StatusIcon(string status) => status switch
    {
        EnrolmentStatuses.Confirmed => "check_circle",
        EnrolmentStatuses.Pending => "schedule",
        EnrolmentStatuses.Cancelled => "cancel",
        "Finished" => "flag",
        _ => "info"
    };

    public static string DaysAway(DateTime date)
    {
        var days = (date.Date - DateTime.Today).Days;
        return days switch
        {
            0 => "Today",
            1 => "Tomorrow",
            > 1 => $"In {days} days",
            -1 => "Yesterday",
            _ => $"{-days} days ago"
        };
    }
}
