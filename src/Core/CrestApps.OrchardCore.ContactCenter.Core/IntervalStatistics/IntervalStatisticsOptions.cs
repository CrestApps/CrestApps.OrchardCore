namespace CrestApps.OrchardCore.ContactCenter.Core.IntervalStatistics;

/// <summary>
/// The options of the interval statistics, bound from <c>CrestApps:ContactCenter:IntervalStatistics</c>.
/// </summary>
public sealed class IntervalStatisticsOptions
{
    /// <summary>
    /// Gets or sets how many days are kept. The default, 760, is two years and a month, enough for a forecast to
    /// compare a day with the same day a year before.
    /// </summary>
    public int RetentionDays { get; set; } = 760;

    /// <summary>
    /// Gets or sets how many days before today are computed when the feature is first turned on.
    /// </summary>
    public int BackfillDays { get; set; } = 400;

    /// <summary>
    /// Gets or sets how many seconds a customer must wait before their abandon counts as more than a short abandon.
    /// </summary>
    public int ShortAbandonSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets how long after midnight a day is computed again for the last time, so work that began before
    /// midnight has ended.
    /// </summary>
    public TimeSpan SettleDelay { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Gets or sets how often today's intervals are computed again.
    /// </summary>
    public TimeSpan CurrentDayRefresh { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Gets or sets the most days one run computes, so a backfill or a rebuild is spread across runs.
    /// </summary>
    public int MaxDaysPerRun { get; set; } = 5;
}
