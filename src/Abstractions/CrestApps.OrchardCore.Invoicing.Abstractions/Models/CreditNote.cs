namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A credit note: a document that reduces or reverses an issued invoice. It cites the invoice by id and number, and its
/// lines name the invoice lines they credit, never more than what is left to credit on each.
/// </summary>
public sealed class CreditNote : InvoicingDocument
{
    /// <summary>
    /// Gets or sets the id of the invoice the credit note credits.
    /// </summary>
    public string InvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the number of the invoice the credit note credits.
    /// </summary>
    public string InvoiceNumber { get; set; }

    /// <summary>
    /// Gets or sets why the invoice is credited, printed on the credit note.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets what the credit note does with the amount it credits.
    /// </summary>
    public CreditNoteDisposition Disposition { get; set; }

    /// <summary>
    /// Gets or sets the life cycle status.
    /// </summary>
    public CreditNoteStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the credit note was voided.
    /// </summary>
    public DateTime? VoidedUtc { get; set; }

    /// <summary>
    /// Gets or sets the amount taken off the invoice's balance.
    /// </summary>
    public decimal AmountApplied { get; set; }

    /// <summary>
    /// Gets or sets the amount given back to the customer.
    /// </summary>
    public decimal AmountRefunded { get; set; }

    /// <summary>
    /// Gets or sets the amount added to the customer's credit.
    /// </summary>
    public decimal AmountCredited { get; set; }
}
