namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// What happened to a bank account.
/// </summary>
public enum BankAccountChangeKind
{
    /// <summary>
    /// The account was added.
    /// </summary>
    Created,

    /// <summary>
    /// Its name, holder, bank or other details changed.
    /// </summary>
    Updated,

    /// <summary>
    /// Its routing or account number changed, which took it back to unverified.
    /// </summary>
    NumberChanged,

    /// <summary>
    /// Its status changed.
    /// </summary>
    StatusChanged,

    /// <summary>
    /// The account was removed.
    /// </summary>
    Deleted,

    /// <summary>
    /// Someone was shown the full account number.
    /// </summary>
    NumberRevealed,
}
