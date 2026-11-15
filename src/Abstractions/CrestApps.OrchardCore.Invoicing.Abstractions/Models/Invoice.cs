namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// An invoice: a document that asks a party to pay. A draft has no number and no effect on any balance. Issuing it
/// numbers it, locks it and posts one transaction to the ledger, whose payments, adjustments and refunds are mirrored back
/// as <see cref="Settlement"/>, <see cref="AmountPaid"/>, <see cref="AmountCredited"/> and <see cref="AmountDue"/>.
/// </summary>
public sealed class Invoice : InvoicingDocument
{
    /// <summary>
    /// Gets or sets the kind of invoice.
    /// </summary>
    public InvoiceKind Kind { get; set; }

    /// <summary>
    /// Gets or sets, on a revision, the id of the invoice it replaces.
    /// </summary>
    public string RevisionOfId { get; set; }

    /// <summary>
    /// Gets or sets, on an invoice that was revised, the id of the invoice that replaced it.
    /// </summary>
    public string ReplacedById { get; set; }

    /// <summary>
    /// Gets or sets, on a debit note, the id of the invoice it relates to.
    /// </summary>
    public string RelatedInvoiceId { get; set; }

    /// <summary>
    /// Gets or sets the life cycle status.
    /// </summary>
    public InvoiceStatus Status { get; set; }

    /// <summary>
    /// Gets or sets how much is paid, mirrored from the transaction.
    /// </summary>
    public InvoiceSettlement Settlement { get; set; }

    /// <summary>
    /// Gets or sets whether the invoice reached the customer.
    /// </summary>
    public InvoiceDelivery Delivery { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the invoice was voided.
    /// </summary>
    public DateTime? VoidedUtc { get; set; }

    /// <summary>
    /// Gets or sets why the invoice was voided.
    /// </summary>
    public string VoidReason { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the invoice was last sent.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the customer first opened the invoice.
    /// </summary>
    public DateTime? ViewedUtc { get; set; }

    /// <summary>
    /// Gets or sets the local date the invoice is due, in the site's time zone.
    /// </summary>
    public DateOnly? DueDate { get; set; }

    /// <summary>
    /// Gets or sets the id of the payment term the due date came from.
    /// </summary>
    public string PaymentTermId { get; set; }

    /// <summary>
    /// Gets or sets the payment term's name, copied at issue.
    /// </summary>
    public string PaymentTermName { get; set; }

    /// <summary>
    /// Gets or sets the id of the transaction the invoice posted when it was issued.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets the amount paid, mirrored from the transaction.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the amount credited or adjusted without money being received, mirrored from the transaction.
    /// </summary>
    public decimal AmountCredited { get; set; }

    /// <summary>
    /// Gets or sets the amount still owed, mirrored from the transaction after issue; the total before.
    /// </summary>
    public decimal AmountDue { get; set; }

    /// <summary>
    /// Gets or sets whether the payer may pay part of the amount due online.
    /// </summary>
    public bool AllowPartialPayment { get; set; }

    /// <summary>
    /// Gets or sets whether the invoice is charged to the payer's saved payment method.
    /// </summary>
    public bool AutoCharge { get; set; }

    /// <summary>
    /// Gets or sets whether reminders are paused for the invoice.
    /// </summary>
    public bool RemindersPaused { get; set; }

    /// <summary>
    /// Gets or sets whether no late fee is charged on the invoice, whatever the late-fee policies say.
    /// </summary>
    public bool LateFeesExempt { get; set; }
}
