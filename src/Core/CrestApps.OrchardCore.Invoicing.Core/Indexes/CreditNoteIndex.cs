using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes;

/// <summary>
/// The columns credit notes are found by, above all the invoice they credit.
/// </summary>
public sealed class CreditNoteIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number, empty for a draft.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the invoice credited.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets when it was issued, in UTC.
    /// </summary>
    public DateTime? IssuedUtc { get; set; }
}

/// <summary>
/// Maps credit notes to <see cref="CreditNoteIndex"/>.
/// </summary>
public sealed class CreditNoteIndexProvider : IndexProvider<CreditNote>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CreditNoteIndexProvider"/> class.
    /// </summary>
    public CreditNoteIndexProvider()
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<CreditNote> context)
    {
        context
            .For<CreditNoteIndex>()
            .Map(note => new CreditNoteIndex
            {
                ItemId = note.ItemId,
                Number = string.IsNullOrEmpty(note.Number) ? null : note.Number,
                InvoiceId = string.IsNullOrEmpty(note.InvoiceId) ? null : note.InvoiceId,
                Status = (int)note.Status,
                Currency = note.Currency,
                Total = note.Totals?.Total ?? 0m,
                IssuedUtc = note.IssuedUtc,
            });
    }
}
