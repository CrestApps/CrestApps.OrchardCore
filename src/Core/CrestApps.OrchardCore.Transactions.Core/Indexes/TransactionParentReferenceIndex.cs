using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Transactions.Models;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// Finds the transactions that belong to a record through <see cref="Transaction.ParentReferenceType"/> and
/// <see cref="Transaction.ParentReferenceId"/>. Only transactions that carry a parent reference have a row.
/// </summary>
/// <remarks>
/// It is a table of its own rather than two more columns on <see cref="TransactionIndex"/>, because almost no
/// transaction has a parent reference, and the ledger's main index is read by every report and sweep.
/// </remarks>
public sealed class TransactionParentReferenceIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the kind of record the transaction belongs to.
    /// </summary>
    public string ParentReferenceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record the transaction belongs to.
    /// </summary>
    public string ParentReferenceId { get; set; }
}
