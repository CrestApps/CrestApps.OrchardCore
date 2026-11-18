using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Transactions.Core.Indexes;

/// <summary>
/// The index of a payment term, in the Transaction collection.
/// </summary>
public sealed class PaymentTermIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the term's name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets whether the term may be chosen for new documents.
    /// </summary>
    public bool Enabled { get; set; }
}
