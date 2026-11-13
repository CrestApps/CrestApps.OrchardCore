namespace CrestApps.OrchardCore.Banking.Models;

/// <summary>
/// A change to a bank account, as the <see cref="Services.IBankAccountObserver"/>s are told about it. It never holds
/// the account number in clear text.
/// </summary>
public sealed class BankAccountChange
{
    /// <summary>
    /// Gets or sets the account as it is now, or as it was when removed.
    /// </summary>
    public BankAccount Account { get; set; }

    /// <summary>
    /// Gets or sets every change one save made, such as a new number and a new status, in that order.
    /// </summary>
    public IReadOnlyList<BankAccountChangeKind> Kinds { get; set; } = [];

    /// <summary>
    /// Gets or sets the status before the change.
    /// </summary>
    public BankAccountStatus? PreviousStatus { get; set; }

    /// <summary>
    /// Gets or sets the user name of who made the change, or <see langword="null"/> for the system.
    /// </summary>
    public string Actor { get; set; }

    /// <summary>
    /// Gets or sets why, when someone said.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Returns whether the change includes a kind.
    /// </summary>
    /// <param name="kind">The kind.</param>
    /// <returns><see langword="true"/> when <paramref name="kind"/> is among <see cref="Kinds"/>.</returns>
    public bool Has(BankAccountChangeKind kind)
        => Kinds.Contains(kind);
}
