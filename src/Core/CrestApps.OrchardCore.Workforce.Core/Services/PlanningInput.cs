using System.Globalization;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reads the times typed into the shift planning editors.
/// </summary>
internal static class PlanningInput
{
    private const int MinutesPerDay = 24 * 60;

    public static bool TryParseTime(string text, out TimeOnly time)
    {
        time = default;

        return !string.IsNullOrWhiteSpace(text)
            && TimeOnly.TryParseExact(text.Trim(), ["HH:mm", "H:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }

    /// <summary>
    /// Returns the minutes from a start to an end time of day; an end at or before the start is on the next day.
    /// </summary>
    public static int LengthMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (int)(((end.ToTimeSpan() - start.ToTimeSpan()).TotalMinutes + MinutesPerDay) % MinutesPerDay);

        return minutes == 0 ? MinutesPerDay : minutes;
    }

    /// <summary>
    /// Returns when a shift on a day ends, from its start and end times of day.
    /// </summary>
    public static (DateTime Start, DateTime End) OnDay(DateOnly date, TimeOnly start, TimeOnly end)
    {
        var from = date.ToDateTime(start);

        return (from, from.AddMinutes(LengthMinutes(start, end)));
    }
}
