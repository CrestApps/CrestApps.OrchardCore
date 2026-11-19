using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.Invoicing.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes;

/// <summary>
/// The index of estimates: what their list, the expiry sweep and the customer's page search.
/// </summary>
public sealed class EstimateIndex : CatalogItemIndex
{
    public string Number { get; set; }

    public int Status { get; set; }

    public string BillToKind { get; set; }

    public string BillToId { get; set; }

    public string BillToName { get; set; }

    public string Reference { get; set; }

    public string Currency { get; set; }

    public decimal Total { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public DateTime? SentUtc { get; set; }

    public DateTime CreatedUtc { get; set; }
}

/// <summary>
/// Maps estimates to <see cref="EstimateIndex"/>.
/// </summary>
public sealed class EstimateIndexProvider : IndexProvider<Estimate>
{
    public EstimateIndexProvider()
    {
        CollectionName = InvoicingConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<Estimate> context)
    {
        context
            .For<EstimateIndex>()
            .ExclusiveToDocumentType()
            .SingleRecordPerDocument()
            .Map(estimate => new EstimateIndex
            {
                ItemId = estimate.ItemId,
                Number = string.IsNullOrEmpty(estimate.Number) ? null : estimate.Number,
                Status = (int)estimate.Status,
                BillToKind = Truncate(estimate.BillTo?.Party.Kind, InvoiceIndexProvider.ShortLength),
                BillToId = Truncate(estimate.BillTo?.Party.Id, InvoiceIndexProvider.PartyIdLength),
                BillToName = Truncate(estimate.BillTo?.Contact?.Name, InvoiceIndexProvider.TextLength),
                Reference = Truncate(estimate.Reference, InvoiceIndexProvider.TextLength),
                Currency = estimate.Currency,
                Total = estimate.Totals?.Total ?? 0m,
                ExpiryDate = estimate.ExpiryDate?.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc),
                SentUtc = estimate.SentUtc,
                CreatedUtc = estimate.CreatedUtc,
            });
    }

    private static string Truncate(string value, int length)
        => string.IsNullOrEmpty(value) ? null : value.Length <= length ? value : value[..length];
}
