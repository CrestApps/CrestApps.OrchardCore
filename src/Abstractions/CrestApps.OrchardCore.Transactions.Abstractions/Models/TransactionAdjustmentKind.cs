namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Why the amount owed on a <see cref="Transaction"/> was lowered without money being received. An adjustment is
/// recorded in <see cref="Transaction.AmountAdjusted"/> and on the timeline as a
/// <see cref="TransactionEventType.Adjusted"/> event.
/// </summary>
public enum TransactionAdjustmentKind
{
    /// <summary>
    /// A price reduction, such as a sibling discount given after the fact or a promotion code.
    /// </summary>
    Discount = 0,

    /// <summary>
    /// Part or all of the charge is waived, such as financial aid.
    /// </summary>
    Waiver = 1,

    /// <summary>
    /// A credit the owner had is applied to the charge.
    /// </summary>
    Credit = 2,

    /// <summary>
    /// The amount is written off as not collectable.
    /// </summary>
    WriteOff = 3,
}
