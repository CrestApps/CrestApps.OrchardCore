using CrestApps.OrchardCore.Customers.Models;
using CrestApps.OrchardCore.Parties.Models;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Services;

/// <summary>
/// The filter applied when querying the transaction ledger. Every property is optional; a property left at
/// its default is not used to constrain the query.
/// </summary>
public sealed class TransactionQuery
{
    /// <summary>
    /// Gets or sets the owner whose transactions are returned.
    /// </summary>
    public string OwnerId { get; set; }

    /// <summary>
    /// Gets or sets the owner kind to filter by, so authenticated and guest obligations can be listed
    /// separately.
    /// </summary>
    public CustomerOwnerKind? OwnerKind { get; set; }

    /// <summary>
    /// Gets or sets the parties whose debts are listed: a transaction is listed when its payer is one of them. When
    /// <see cref="OwnerId"/> is set too, a transaction is listed when it has that owner or one of these payers, which is
    /// how a person sees their own debts and those of the accounts they act for.
    /// </summary>
    public IList<PartyRef> Payers { get; set; } = [];

    /// <summary>
    /// Gets or sets the exact status to filter by.
    /// </summary>
    public TransactionStatus? Status { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether only outstanding transactions (those with a balance still
    /// owed) are returned. When <see langword="true"/> it takes precedence over <see cref="Status"/>.
    /// </summary>
    public bool OutstandingOnly { get; set; }

    /// <summary>
    /// Gets or sets the origin/provider key to filter by.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the reference type to filter by.
    /// </summary>
    public string ReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the reference id to filter by.
    /// </summary>
    public string ReferenceId { get; set; }

    /// <summary>
    /// Gets or sets a case-insensitive title search term.
    /// </summary>
    public string Search { get; set; }
}
