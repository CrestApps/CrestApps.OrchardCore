using System.Globalization;

namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// Provides shared value formatting helpers used when building report documents so numbers, durations,
/// and rates render consistently across every report.
/// </summary>
public static class ReportFormat
{
    /// <summary>
    /// Formats a number of seconds as a compact, human-readable duration (for example <c>1h 03m</c>).
    /// </summary>
    /// <param name="seconds">The duration in seconds.</param>
    /// <returns>The formatted duration.</returns>
    public static string Duration(double seconds)
    {
        if (seconds <= 0)
        {
            return "0s";
        }

        var total = (long)Math.Round(seconds, MidpointRounding.AwayFromZero);
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var secs = total % 60;

        if (hours > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{hours}h {minutes:00}m");
        }

        if (minutes > 0)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{minutes}m {secs:00}s");
        }

        return string.Create(CultureInfo.InvariantCulture, $"{secs}s");
    }

    /// <summary>
    /// Formats a rate between 0 and 1 as a percentage string (for example <c>42.5%</c>).
    /// </summary>
    /// <param name="rate">The rate to format.</param>
    /// <returns>The formatted percentage.</returns>
    public static string Percent(double rate)
    {
        return string.Create(CultureInfo.InvariantCulture, $"{Math.Round(rate * 100, 1):0.0}%");
    }

    /// <summary>
    /// Formats an integer value using the invariant culture.
    /// </summary>
    /// <param name="value">The value to format.</param>
    /// <returns>The formatted number.</returns>
    public static string Number(long value)
    {
        return value.ToString(CultureInfo.InvariantCulture);
    }
}
