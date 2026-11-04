namespace CrestApps.OrchardCore.Workforce.Models;

/// <summary>
/// Why a time-off balance changed.
/// </summary>
public enum TimeOffLedgerKind
{
    /// <summary>
    /// The balance a worker started with, such as one brought from another system.
    /// </summary>
    Opening,

    /// <summary>
    /// Added by the policy.
    /// </summary>
    Accrual,

    /// <summary>
    /// Approved time off.
    /// </summary>
    Taken,

    /// <summary>
    /// Approved time off that was canceled, given back.
    /// </summary>
    Reversal,

    /// <summary>
    /// A correction by a manager.
    /// </summary>
    Adjustment,

    /// <summary>
    /// Moved from the year before.
    /// </summary>
    Carryover,

    /// <summary>
    /// Lost at the reset date, above what may be carried over.
    /// </summary>
    Expiry,

    /// <summary>
    /// Paid out instead of taken.
    /// </summary>
    Payout,

    /// <summary>
    /// Given with a last day to use it, such as year-end bonus hours. It is used before the rest of the balance, and
    /// what is left of it expires the day after.
    /// </summary>
    Grant,
}
