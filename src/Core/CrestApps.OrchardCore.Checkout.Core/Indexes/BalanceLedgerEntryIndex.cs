using CrestApps.OrchardCore.Checkout.Core.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Checkout.Core.Indexes;

/// <summary>
/// Finds the moved-out changes of a balance, and one change by its reference, with a keyed lookup.
/// </summary>
public sealed class BalanceLedgerEntryIndex : MapIndex
{
    /// <summary>
    /// Gets or sets the kind of balance.
    /// </summary>
    public string Ledger { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the balance's document.
    /// </summary>
    public string AccountId { get; set; }

    /// <summary>
    /// Gets or sets the reference of the change, if any.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets when the balance changed, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps <see cref="BalanceLedgerEntryRecord"/> to <see cref="BalanceLedgerEntryIndex"/>.
/// </summary>
public sealed class BalanceLedgerEntryIndexProvider : IndexProvider<BalanceLedgerEntryRecord>
{
    /// <summary>
    /// The length of the reference column; longer references are cut, as the store cuts them when it looks one up.
    /// </summary>
    public const int ReferenceLength = 191;

    /// <inheritdoc/>
    public override void Describe(DescribeContext<BalanceLedgerEntryRecord> context)
    {
        context
            .For<BalanceLedgerEntryIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(record => new BalanceLedgerEntryIndex
            {
                Ledger = record.Ledger,
                AccountId = record.AccountId,
                Reference = Cut(record.Reference),
                CreatedUtc = record.CreatedUtc,
            });
    }

    /// <summary>
    /// Returns a reference as the index keeps it.
    /// </summary>
    /// <param name="reference">The reference.</param>
    public static string Cut(string reference)
        => string.IsNullOrEmpty(reference) ? null : reference.Length <= ReferenceLength ? reference : reference[..ReferenceLength];
}
