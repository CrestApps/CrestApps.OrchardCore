namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// How often workers are paid.
/// </summary>
public enum PayPeriodFrequency
{
    /// <summary>
    /// Every week.
    /// </summary>
    Weekly,

    /// <summary>
    /// Every two weeks.
    /// </summary>
    Biweekly,

    /// <summary>
    /// Twice a month: the 1st to the 15th, and the 16th to the end of the month.
    /// </summary>
    SemiMonthly,

    /// <summary>
    /// Every calendar month.
    /// </summary>
    Monthly,
}
