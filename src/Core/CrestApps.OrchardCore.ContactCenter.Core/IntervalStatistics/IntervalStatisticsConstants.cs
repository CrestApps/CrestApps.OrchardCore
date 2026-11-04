namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// The constants of the interval statistics.
/// </summary>
public static class IntervalStatisticsConstants
{
    /// <summary>
    /// The length of an interval in minutes.
    /// </summary>
    public const int IntervalMinutes = 15;

    /// <summary>
    /// The version of the computation. A day computed by another version is computed again.
    /// </summary>
    public const int Version = 1;

    /// <summary>
    /// The length of an interval.
    /// </summary>
    public static readonly TimeSpan Interval = TimeSpan.FromMinutes(IntervalMinutes);

    /// <summary>
    /// Gets the start of the interval an instant falls in.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <param name="minutes">The interval length in minutes: 15, 30 or 60.</param>
    /// <returns>The interval's UTC start.</returns>
    public static DateTime IntervalStart(DateTime utc, int minutes = IntervalMinutes)
    {
        var ticks = TimeSpan.FromMinutes(minutes).Ticks;

        return new DateTime(utc.Ticks - (utc.Ticks % ticks), DateTimeKind.Utc);
    }

    /// <summary>
    /// Gets the UTC midnight that starts the day an instant falls on.
    /// </summary>
    /// <param name="utc">The instant.</param>
    /// <returns>The start of its UTC day.</returns>
    public static DateTime DayStart(DateTime utc)
        => DateTime.SpecifyKind(utc.Date, DateTimeKind.Utc);
}
