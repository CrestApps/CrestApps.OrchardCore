namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// What a <see cref="DocumentLine"/> bills. <see cref="Text"/> and <see cref="Section"/> lines carry no amount.
/// </summary>
public enum DocumentLineKind
{
    /// <summary>
    /// A product or another item.
    /// </summary>
    Item = 0,

    /// <summary>
    /// A service.
    /// </summary>
    Service = 1,

    /// <summary>
    /// Time worked.
    /// </summary>
    Time = 2,

    /// <summary>
    /// An expense passed on to the customer.
    /// </summary>
    Expense = 3,

    /// <summary>
    /// Metered usage.
    /// </summary>
    Usage = 4,

    /// <summary>
    /// A fee, such as a late fee.
    /// </summary>
    Fee = 5,

    /// <summary>
    /// A deposit, or the deduction of a deposit paid earlier (a negative amount).
    /// </summary>
    Deposit = 6,

    /// <summary>
    /// A line of text with no amount.
    /// </summary>
    Text = 7,

    /// <summary>
    /// A heading that starts a group of lines, with no amount.
    /// </summary>
    Section = 8,
}
