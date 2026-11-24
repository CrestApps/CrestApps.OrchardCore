namespace CrestApps.OrchardCore.YesSql.Core.Ledgers;

/// <summary>
/// Where a ledger is kept.
/// </summary>
/// <typeparam name="TLedger">The marker type of the ledger.</typeparam>
public sealed class QuantityLedgerOptions<TLedger>
{
    /// <summary>
    /// Gets or sets the YesSql collection of the ledger.
    /// </summary>
    public string CollectionName { get; set; }
}
