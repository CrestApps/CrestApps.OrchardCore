namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// Where a bank account is in its verification. <see cref="BankAccountStatuses.Transitions"/> says which moves are
/// allowed.
/// </summary>
public enum BankAccountStatus
{
    /// <summary>
    /// Entered, or its number changed, and not checked yet.
    /// </summary>
    Unverified,

    /// <summary>
    /// A check was started, such as a zero-amount test entry, and no answer came back yet.
    /// </summary>
    VerificationSent,

    /// <summary>
    /// Checked: payments can go to it.
    /// </summary>
    Verified,

    /// <summary>
    /// The check failed, or the bank returned a payment: the account is wrong or closed at the bank.
    /// </summary>
    Failed,

    /// <summary>
    /// No longer used. A closed account never opens again; add a new one instead.
    /// </summary>
    Closed,
}
