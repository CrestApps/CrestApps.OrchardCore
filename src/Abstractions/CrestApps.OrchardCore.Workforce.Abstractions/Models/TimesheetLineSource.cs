namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Where a timesheet line came from.
/// </summary>
public enum TimesheetLineSource
{
    /// <summary>
    /// Worked time from punches.
    /// </summary>
    Punches,

    /// <summary>
    /// Approved time off.
    /// </summary>
    TimeOff,

    /// <summary>
    /// Approved predictability pay.
    /// </summary>
    PredictabilityPay,

    /// <summary>
    /// Added by a manager.
    /// </summary>
    Manual,
}
