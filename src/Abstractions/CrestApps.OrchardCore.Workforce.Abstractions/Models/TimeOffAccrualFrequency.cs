namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How long a time-off policy's accrual period is.
/// </summary>
public enum TimeOffAccrualFrequency
{
    /// <summary>
    /// Every week, from the policy's anchor date.
    /// </summary>
    Weekly,

    /// <summary>
    /// Every two weeks, from the policy's anchor date.
    /// </summary>
    Biweekly,

    /// <summary>
    /// The 1st to the 15th, and the 16th to the end of the month.
    /// </summary>
    SemiMonthly,

    /// <summary>
    /// Each calendar month.
    /// </summary>
    Monthly,
}
