namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// What each <see cref="BankAccountKind"/> can be used for.
/// </summary>
public static class BankAccountKinds
{
    /// <summary>
    /// Returns whether an account of a kind is a deposit account (checking or savings): the only kinds a bank file
    /// (ACH) sends money from or to.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for <see cref="BankAccountKind.Checking"/> and <see cref="BankAccountKind.Savings"/>.</returns>
    public static bool IsDeposit(BankAccountKind kind)
        => kind is BankAccountKind.Checking or BankAccountKind.Savings;

    /// <summary>
    /// Returns whether checks can be drawn on an account of a kind: a deposit account, checking or savings.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> for <see cref="BankAccountKind.Checking"/> and <see cref="BankAccountKind.Savings"/>.</returns>
    public static bool CanDrawChecks(BankAccountKind kind)
        => IsDeposit(kind);
}
