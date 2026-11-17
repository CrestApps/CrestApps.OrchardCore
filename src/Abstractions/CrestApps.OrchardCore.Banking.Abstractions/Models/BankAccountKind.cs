namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// The kind of a bank account, which a bank file states for every payment.
/// </summary>
public enum BankAccountKind
{
    /// <summary>
    /// A checking (current) account.
    /// </summary>
    Checking,

    /// <summary>
    /// A savings account.
    /// </summary>
    Savings,
}
