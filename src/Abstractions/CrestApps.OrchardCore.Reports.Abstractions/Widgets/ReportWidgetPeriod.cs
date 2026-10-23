namespace CrestApps.OrchardCore.Reports;

/// <summary>
/// The period a report dashboard widget covers. Every period but <see cref="Custom"/> rolls with the clock, so a
/// widget set to the last 30 days always shows the 30 days up to now.
/// </summary>
public enum ReportWidgetPeriod
{
    /// <summary>
    /// Since the start of today.
    /// </summary>
    Today,

    /// <summary>
    /// Yesterday, the whole day.
    /// </summary>
    Yesterday,

    /// <summary>
    /// The last 7 days, including today.
    /// </summary>
    Last7Days,

    /// <summary>
    /// The last 30 days, including today.
    /// </summary>
    Last30Days,

    /// <summary>
    /// The last 90 days, including today.
    /// </summary>
    Last90Days,

    /// <summary>
    /// Since the first of this month.
    /// </summary>
    ThisMonth,

    /// <summary>
    /// The whole of last month.
    /// </summary>
    LastMonth,

    /// <summary>
    /// Since the first of January.
    /// </summary>
    ThisYear,

    /// <summary>
    /// The last 12 months, including this one.
    /// </summary>
    Last12Months,

    /// <summary>
    /// Everything, with no start.
    /// </summary>
    AllTime,

    /// <summary>
    /// The dates the widget was given.
    /// </summary>
    Custom,
}
