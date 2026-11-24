namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// The kind of a bank account. A bank file states it for every payment, and <see cref="BankAccountKinds"/> says what
/// each kind can be used for: only a deposit account (checking or savings) sends or receives a bank file or has checks
/// drawn on it.
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

    /// <summary>
    /// A credit card account.
    /// </summary>
    CreditCard,

    /// <summary>
    /// A line of credit, such as a business credit line at a bank.
    /// </summary>
    LineOfCredit,

    /// <summary>
    /// Cash the business keeps, such as petty cash or the money in a till: no bank holds it.
    /// </summary>
    Cash,

    /// <summary>
    /// Any other kind of account, such as a loan or a payment service balance.
    /// </summary>
    Other,
}
