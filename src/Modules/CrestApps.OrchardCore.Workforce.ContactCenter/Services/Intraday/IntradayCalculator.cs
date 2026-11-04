using CrestApps.OrchardCore.Workforce.ContactCenter.Models;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Services.Intraday;

/// <summary>
/// The pure parts of intraday management: how far the day is running from its forecast, and the quietest times.
/// </summary>
public static class IntradayCalculator
{
    /// <summary>
    /// Gets the factor to scale the rest of the day by: the actual work over the forecast so far, damped toward one.
    /// </summary>
    /// <param name="rows">The day's rows.</param>
    /// <param name="damping">How much of the difference to follow, from 0 (none) to 1 (all of it).</param>
    /// <returns>The factor, between 0.25 and 4; one when nothing is known yet.</returns>
    public static double ReforecastFactor(IEnumerable<IntradayRow> rows, double damping = 0.5)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var elapsed = rows.Where(row => row.Elapsed && row.ActualVolume.HasValue && row.ForecastVolume > 0).ToArray();
        var forecast = elapsed.Sum(row => row.ForecastVolume);

        if (forecast <= 0)
        {
            return 1;
        }

        var ratio = elapsed.Sum(row => row.ActualVolume.Value) / forecast;

        return Math.Clamp(1 + (Math.Clamp(damping, 0, 1) * (ratio - 1)), 0.25, 4);
    }

    /// <summary>
    /// Finds the times to take some people off the phones that add the least shortfall, such as for a meeting.
    /// </summary>
    /// <param name="rows">The day's rows, in order.</param>
    /// <param name="people">How many people.</param>
    /// <param name="minutes">For how long.</param>
    /// <param name="fromUtc">The earliest start.</param>
    /// <param name="count">How many times to return.</param>
    /// <returns>The best times, least shortfall first, then earliest.</returns>
    public static IReadOnlyList<ActivityWindow> FindWindows(IReadOnlyList<IntradayRow> rows, int people, int minutes, DateTime fromUtc, int count = 3)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var length = Math.Max(1, (int)Math.Ceiling(minutes / 15d));
        var windows = new List<ActivityWindow>();
        var ordered = rows.OrderBy(row => row.StartUtc).ToArray();

        for (var start = 0; start + length <= ordered.Length; start++)
        {
            if (ordered[start].StartUtc < fromUtc || ordered[start + length - 1].StartUtc != ordered[start].StartUtc.AddMinutes(15 * (length - 1)))
            {
                continue;
            }

            var window = ordered.Skip(start).Take(length).ToArray();

            // Only times with people scheduled can spare them.
            if (window.Any(row => row.Scheduled < people))
            {
                continue;
            }

            var added = window.Sum(row => Math.Max(0, row.Required - (row.Scheduled - people)) - Math.Max(0, row.Required - row.Scheduled));

            windows.Add(new ActivityWindow(window[0].StartUtc, window[^1].StartUtc.AddMinutes(15), added));
        }

        return [.. windows.OrderBy(window => window.AddedShortfall).ThenBy(window => window.StartUtc).Take(count)];
    }
}
