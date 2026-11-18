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

    /// <summary>
    /// Gets or sets the billable work and costs the invoice bills, each with the line that bills it, so they are let go
    /// when the line is taken off or the invoice is voided.
    /// </summary>
    public IList<InvoiceBilledItem> BilledItems { get; set; } = [];

    /// <summary>
    /// Gets or sets who submitted the invoice for approval, and who approved or rejected it, when it needed approval.
    /// </summary>
    public InvoiceApproval Approval { get; set; }

    /// <summary>
    /// Gets or sets the payment schedule set by staff: when each part falls due and how much, by amount or by percent of
    /// the total. Once issued, the invoice's debt is split into these payments on the ledger by an installment plan.
    /// </summary>
    public IList<InvoiceInstallment> Schedule { get; set; } = [];

    /// <summary>
    /// Gets or sets the installment plan that collects the schedule, once the invoice is issued.
    /// </summary>
    public string InstallmentPlanId { get; set; }
}

/// <summary>
/// One part of an invoice's payment schedule.
/// </summary>
public sealed class InvoiceInstallment
{
    public DateOnly DueDate { get; set; }

    /// <summary>
    /// Gets or sets the amount, when the part is set by amount.
    /// </summary>
    public decimal? Amount { get; set; }

    /// <summary>
    /// Gets or sets the percent of the total, when the part is set by percent.
    /// </summary>
    public decimal? Percent { get; set; }

    /// <summary>
    /// Gets or sets the payment's number in the installment plan, once issued.
    /// </summary>
    public int Number { get; set; }

    /// <summary>
    /// Gets or sets the ledger transaction of the payment, once issued.
    /// </summary>
    public string TransactionId { get; set; }

    /// <summary>
    /// Gets or sets what the payment comes to, worked out at issue.
    /// </summary>
    public decimal IssuedAmount { get; set; }
}

/// <summary>
/// The approval of an invoice at or above the approval amount: who asked, and who approved or rejected it.
/// </summary>
public sealed class InvoiceApproval
{
    public string SubmittedBy { get; set; }

    public DateTime? SubmittedUtc { get; set; }

    public string ApprovedBy { get; set; }

    public DateTime? ApprovedUtc { get; set; }

    public string RejectedBy { get; set; }

    public DateTime? RejectedUtc { get; set; }

    public string RejectionReason { get; set; }
}

/// <summary>
/// An item of a billable source an invoice bills.
/// </summary>
public sealed class InvoiceBilledItem
{
    /// <summary>
    /// Gets or sets the source's type, such as <c>charges</c> or <c>time</c>.
    /// </summary>
    public string Source { get; set; }

    /// <summary>
    /// Gets or sets the item's id in the source.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the line that bills it.
    /// </summary>
    public string LineId { get; set; }
}
