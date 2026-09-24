using System.Globalization;

namespace CrestApps.OrchardCore.ContactCenter.Reports.Services;

/// <summary>
/// Formats the values of the reports built on the event log, where a second, and sometimes a millisecond, matters.
/// </summary>
internal static class AuditReportFormat
{
    /// <summary>
    /// Formats an instant to the millisecond.
    /// </summary>
    /// <param name="value">The instant, in UTC.</param>
    /// <returns>The formatted instant.</returns>
    public static string Timestamp(DateTime value)
        => value.ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a time of day to the second.
    /// </summary>
    /// <param name="value">The instant, in UTC.</param>
    /// <returns>The formatted time of day.</returns>
    public static string TimeOfDay(DateTime value)
        => value.ToString("HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a whole number of seconds as hours, minutes and seconds, with the hours unbounded, so a column of
    /// them can be added up by hand.
    /// </summary>
    /// <param name="seconds">The number of seconds.</param>
    /// <returns>The formatted duration, such as <c>7:59:03</c>.</returns>
    public static string Clock(long seconds)
    {
        var sign = seconds < 0 ? "-" : string.Empty;
        var magnitude = Math.Abs(seconds);

        return string.Create(CultureInfo.InvariantCulture, $"{sign}{magnitude / 3600}:{magnitude % 3600 / 60:00}:{magnitude % 60:00}");
    }

    /// <summary>
    /// Formats a duration in seconds as hours, minutes and seconds, rounded to the second.
    /// </summary>
    /// <param name="seconds">The number of seconds.</param>
    /// <returns>The formatted duration.</returns>
    public static string Clock(double seconds)
        => Clock((long)Math.Round(seconds, MidpointRounding.AwayFromZero));

    /// <summary>
    /// Rounds durations to whole seconds so that the rounded parts add up to the rounded whole, as a timecard's
    /// columns must: rounding each part on its own can leave the parts a second off their total.
    /// </summary>
    /// <param name="parts">The durations, in seconds.</param>
    /// <returns>The whole seconds of each part, which add up to the rounded total of the parts.</returns>
    public static long[] RoundToWholeSeconds(IReadOnlyList<double> parts)
    {
        ArgumentNullException.ThrowIfNull(parts);

        var total = (long)Math.Round(parts.Sum(), MidpointRounding.AwayFromZero);
        var floors = parts.Select(part => (long)Math.Floor(part)).ToArray();
        var remainder = total - floors.Sum();

        // The seconds the floors lost go to the parts that lost the most, the largest remainder method.
        foreach (var index in Enumerable.Range(0, parts.Count)
            .OrderByDescending(index => parts[index] - Math.Floor(parts[index]))
            .ThenBy(index => index))
        {
            if (remainder <= 0)
            {
                break;
            }

            floors[index]++;
            remainder--;
        }

        return floors;
    }
}
