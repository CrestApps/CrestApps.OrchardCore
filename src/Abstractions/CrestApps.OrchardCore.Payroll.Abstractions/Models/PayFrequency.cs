namespace CrestApps.OrchardCore.Payroll.Models;

/// <summary>
/// How often a pay period starts. The first four keep the values Workforce's pay periods had.
/// </summary>
public enum PayFrequency
{
    /// <summary>
    /// Every week, from the anchor date.
    /// </summary>
    Weekly,

    /// <summary>
    /// Every two weeks, from the anchor date.
    /// </summary>
    Biweekly,

    /// <summary>
    /// Twice a month: from the first day, and from the second period's start day to the end of the month.
    /// </summary>
    SemiMonthly,

    /// <summary>
    /// Every calendar month.
    /// </summary>
    Monthly,

    /// <summary>
    /// Every four weeks, from the anchor date.
    /// </summary>
    FourWeekly,

    /// <summary>
    /// Every quarter of the year, from the anchor date's month.
    /// </summary>
    Quarterly,

    /// <summary>
    /// Every year, from the anchor date's month.
    /// </summary>
    Yearly,
}

/// <summary>
/// Where a pay date that falls on a day off moves.
/// </summary>
public enum PayDateShift
{
    /// <summary>
    /// The business day before.
    /// </summary>
    Before,

    /// <summary>
    /// The business day after.
    /// </summary>
    After,

    /// <summary>
    /// It stays.
    /// </summary>
    None,
}

/// <summary>
/// How a pay date is found.
/// </summary>
public enum PayDateRule
{
    /// <summary>
    /// A number of days after the period ends.
    /// </summary>
    DaysAfterPeriodEnd,

    /// <summary>
    /// Fixed days of the month (monthly and twice-a-month schedules), such as the 15th and the last day.
    /// </summary>
    FixedDays,
}
