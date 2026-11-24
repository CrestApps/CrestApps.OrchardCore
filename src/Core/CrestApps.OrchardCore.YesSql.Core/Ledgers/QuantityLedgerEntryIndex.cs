using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Ledgers;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.YesSql.Core.Ledgers;

/// <summary>
/// Finds ledger entries by balance, kind and key.
/// </summary>
public sealed class QuantityLedgerEntryIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the account.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the balance of the account.
    /// </summary>
    public string Bucket { get; set; }

    /// <summary>
    /// Gets or sets the kind.
    /// </summary>
    public string Kind { get; set; }

    /// <summary>
    /// Gets or sets the effective day, as yyyyMMdd.
    /// </summary>
    public int EffectiveDate { get; set; }

    /// <summary>
    /// Gets or sets the expiry, as yyyyMMdd.
    /// </summary>
    public int? ExpiresOn { get; set; }

    /// <summary>
    /// Gets or sets the idempotency key.
    /// </summary>
    public string IdempotencyKey { get; set; }

    /// <summary>
    /// Gets or sets when the entry was posted.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Indexes the entries of one ledger collection.
/// </summary>
public sealed class QuantityLedgerEntryIndexProvider : IndexProvider<QuantityLedgerEntry>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="QuantityLedgerEntryIndexProvider"/> class.
    /// </summary>
    /// <param name="collectionName">The collection of the ledger.</param>
    public QuantityLedgerEntryIndexProvider(string collectionName)
    {
        CollectionName = collectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<QuantityLedgerEntry> context)
    {
        context.For<QuantityLedgerEntryIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(entry => new QuantityLedgerEntryIndex
            {
                ItemId = entry.ItemId,
                AccountId = entry.AccountId,
                Bucket = entry.Bucket,
                Kind = entry.Kind,
                EffectiveDate = QuantityLedgerDates.ToKey(entry.EffectiveDate),
                ExpiresOn = entry.ExpiresOn is { } expiresOn ? QuantityLedgerDates.ToKey(expiresOn) : null,
                IdempotencyKey = entry.IdempotencyKey,
                CreatedUtc = entry.CreatedUtc,
            });
    }
}

/// <summary>
/// Stores days as sortable numbers.
/// </summary>
public static class QuantityLedgerDates
{
    /// <summary>
    /// Turns a day into yyyyMMdd.
    /// </summary>
    /// <param name="date">The day.</param>
    /// <returns>The number.</returns>
    public static int ToKey(DateOnly date) => (date.Year * 10000) + (date.Month * 100) + date.Day;
}
