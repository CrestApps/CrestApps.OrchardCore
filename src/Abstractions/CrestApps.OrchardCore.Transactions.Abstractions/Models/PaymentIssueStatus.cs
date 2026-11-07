namespace CrestApps.OrchardCore.Transactions.Models;

/// <summary>
/// Whether a <see cref="PaymentIssue"/> still needs an operator.
/// </summary>
public enum PaymentIssueStatus
{
    /// <summary>
    /// It needs attention.
    /// </summary>
    Open = 0,

    /// <summary>
    /// An operator finished it by hand.
    /// </summary>
    Resolved = 1,
}
