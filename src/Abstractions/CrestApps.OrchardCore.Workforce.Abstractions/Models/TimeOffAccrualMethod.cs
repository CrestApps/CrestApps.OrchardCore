namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How a time-off policy adds to a worker's balance.
/// </summary>
public enum TimeOffAccrualMethod
{
    /// <summary>
    /// Nothing is added; the balance changes only by adjustments.
    /// </summary>
    None,

    /// <summary>
    /// A set amount at the end of every accrual period.
    /// </summary>
    PerPeriod,

    /// <summary>
    /// An amount for each hour worked in an accrual period.
    /// </summary>
    PerHourWorked,

    /// <summary>
    /// The year's amount at once on the reset date, and a share of it when the worker becomes eligible.
    /// </summary>
    AnnualLumpSum,
}
