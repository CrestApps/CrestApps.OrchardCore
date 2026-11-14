using CrestApps.Core.Data.YesSql.Indexes;

namespace CrestApps.OrchardCore.Invoicing.Core.Indexes;

/// <summary>
/// The columns invoice lists filter and sort on, so a list never loads a document to filter it.
/// </summary>
public sealed class InvoiceIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the number, empty for a draft.
    /// </summary>
    public string Number { get; set; }

    /// <summary>
    /// Gets or sets the status.
    /// </summary>
    public int Status { get; set; }

    /// <summary>
    /// Gets or sets the settlement.
    /// </summary>
    public int Settlement { get; set; }

    /// <summary>
    /// Gets or sets the kind of the party billed.
    /// </summary>
    public string BillToKind { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the party billed.
    /// </summary>
    public string BillToId { get; set; }

    /// <summary>
    /// Gets or sets the bill-to's name, for search.
    /// </summary>
    public string BillToName { get; set; }

    /// <summary>
    /// Gets or sets the reference and the purchase order number, for search.
    /// </summary>
    public string Reference { get; set; }

    /// <summary>
    /// Gets or sets the currency.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets the total.
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// Gets or sets what is still due, as mirrored from the transaction.
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Gets or sets the due date, as midnight of that date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Gets or sets when it was issued, in UTC.
    /// </summary>
    public DateTime? IssuedUtc { get; set; }

    /// <summary>
    /// Gets or sets when it was created, in UTC.
    /// </summary>
    public DateTime CreatedUtc { get; set; }

    /// <summary>
    /// Gets or sets the transaction it posted.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the kind of record it was made from.
    /// </summary>
    public string SourceType { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the record it was made from.
    /// </summary>
    public string SourceId { get; set; }
}
