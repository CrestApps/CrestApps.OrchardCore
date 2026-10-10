namespace CrestApps.OrchardCore.Reports.Designer.Models;

/// <summary>
/// The refresh schedules a <see cref="ReportView"/> can use, in minutes. A scheduled view stores its result so the
/// reports that read it do not run it again; a live view runs every time a report reads it.
/// </summary>
public static class ReportViewRefreshIntervals
{
    /// <summary>
    /// The view runs every time a report reads it.
    /// </summary>
    public const int Live = 0;

    /// <summary>
    /// The shortest schedule, in minutes.
    /// </summary>
    public const int Minimum = 15;

    /// <summary>
    /// Every hour.
    /// </summary>
    public const int Hourly = 60;

    /// <summary>
    /// Every six hours.
    /// </summary>
    public const int EverySixHours = 360;

    /// <summary>
    /// Every day.
    /// </summary>
    public const int Daily = 1440;

    /// <summary>
    /// Gets the schedules the builder offers, in minutes, live first.
    /// </summary>
    public static IReadOnlyList<int> Options { get; } = [Live, Minimum, Hourly, EverySixHours, Daily];

    /// <summary>
    /// Normalizes a schedule: anything not positive is live, and a schedule is never shorter than
    /// <see cref="Minimum"/>.
    /// </summary>
    /// <param name="minutes">The requested schedule, in minutes.</param>
    /// <returns>The schedule to store.</returns>
    public static int Normalize(int minutes)
    {
        return minutes <= 0 ? Live : Math.Max(Minimum, minutes);
    }
}
