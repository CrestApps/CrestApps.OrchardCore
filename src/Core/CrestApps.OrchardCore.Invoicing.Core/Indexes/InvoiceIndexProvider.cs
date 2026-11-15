using CrestApps.OrchardCore.Invoicing.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes;

/// <summary>
/// Maps invoices to <see cref="InvoiceIndex"/>.
/// </summary>
public sealed class InvoiceIndexProvider : IndexProvider<Invoice>
{
    /// <summary>
    /// The length of the short text columns.
    /// </summary>
    public const int ShortLength = 64;

    /// <summary>
    /// The length of the identifier columns.
    /// </summary>
    public const int IdLength = 26;

    /// <summary>
    /// The length of the party identifier column, which holds content item ids and user ids.
    /// </summary>
    public const int PartyIdLength = 64;

    /// <summary>
    /// The length of the searchable text columns.
    /// </summary>
    public const int TextLength = 255;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvoiceIndexProvider"/> class.
    /// </summary>
    public InvoiceIndexProvider()
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Invoice> context)
    {
        context
            .For<InvoiceIndex>()
            .Map(invoice => new InvoiceIndex
            {
                ItemId = invoice.ItemId,
                Number = Truncate(invoice.Number, ShortLength),
                Status = (int)invoice.Status,
                Settlement = (int)invoice.Settlement,
                BillToKind = Truncate(invoice.BillTo?.Party.Kind, ShortLength),
                BillToId = Truncate(invoice.BillTo?.Party.Id, PartyIdLength),
                BillToName = Truncate(invoice.BillTo?.Contact?.Name, TextLength),
                BillToEmail = Truncate(invoice.BillTo?.Contact?.Email?.Trim().ToLowerInvariant(), TextLength),
                Reference = Truncate(string.Join(' ', new[] { invoice.Reference, invoice.PurchaseOrderNumber }.Where(value => !string.IsNullOrWhiteSpace(value))), TextLength),
                Currency = Truncate(invoice.Currency, 3),
                Total = invoice.Totals?.Total ?? 0m,
                AmountDue = invoice.AmountDue,
                DueDate = invoice.DueDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                IssuedUtc = invoice.IssuedUtc,
                CreatedUtc = invoice.CreatedUtc,
                TransactionId = Truncate(invoice.TransactionId, IdLength),
                SourceType = Truncate(invoice.SourceType, ShortLength),
                SourceId = Truncate(invoice.SourceId, PartyIdLength),
                RelatedInvoiceId = Truncate(invoice.RelatedInvoiceId, IdLength),
            });
    }

    private static string Truncate(string value, int length)
        => string.IsNullOrEmpty(value) || value.Length <= length
            ? (string.IsNullOrEmpty(value) ? null : value)
            : value[..length];
}
