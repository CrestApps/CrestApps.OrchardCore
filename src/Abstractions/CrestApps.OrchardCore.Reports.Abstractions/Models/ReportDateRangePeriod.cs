namespace CrestApps.OrchardCore.Reports.Models;

/// <summary>
/// The kinds of period a report can start on, before a person picks a date range of their own.
/// </summary>
public enum ReportDateRangePeriod
{
    /// <summary>
    /// Today, from the start to the end of the day. This is the period of a report that declares none.
    /// </summary>
    Today,

    /// <summary>
    /// Every date: neither bound is set.
    /// </summary>
    AllTime,

    /// <summary>
    /// The last days up to the end of today, today included.
    /// </summary>
    LastDays,

    /// <summary>
    /// The current week, from its first day to the end of its seventh day.
    /// </summary>
    ThisWeek,
}
