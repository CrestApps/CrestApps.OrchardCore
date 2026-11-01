namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// What a pay code pays for.
/// </summary>
public enum PayCodeKind
{
    /// <summary>
    /// Ordinary worked hours.
    /// </summary>
    Regular,

    /// <summary>
    /// Hours past the overtime thresholds.
    /// </summary>
    Overtime,

    /// <summary>
    /// Hours past the double-time threshold.
    /// </summary>
    DoubleTime,

    /// <summary>
    /// Holiday hours.
    /// </summary>
    Holiday,

    /// <summary>
    /// Paid time off.
    /// </summary>
    PaidTimeOff,

    /// <summary>
    /// Premium for late schedule changes.
    /// </summary>
    PredictabilityPay,

    /// <summary>
    /// Being called back to work.
    /// </summary>
    CallBack,

    /// <summary>
    /// A premium for hours inside a window, such as nights or weekends, paid on top of the hours.
    /// </summary>
    ShiftDifferential,

    /// <summary>
    /// Time that is recorded but not paid.
    /// </summary>
    Unpaid,
}
