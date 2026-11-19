namespace CrestApps.OrchardCore.Invoicing.Models;

/// <summary>
/// A priced offer sent to a customer before the work: the same header and lines as an invoice, numbered when it is sent,
/// accepted or declined by the customer, and turned into an invoice.
/// </summary>
public sealed class Estimate : InvoicingDocument
{
    /// <summary>
    /// Gets or sets where the estimate stands.
    /// </summary>
    public EstimateStatus Status { get; set; }

    /// <summary>
    /// Gets or sets the last day the offer can be accepted, a date in the site's time zone.
    /// </summary>
    public DateOnly? ExpiryDate { get; set; }

    /// <summary>
    /// Gets or sets the percent of the total asked as a deposit, such as 30.
    /// </summary>
    public decimal? DepositPercent { get; set; }

    /// <summary>
    /// Gets or sets the amount asked as a deposit, used instead of <see cref="DepositPercent"/> when set.
    /// </summary>
    public decimal? DepositAmount { get; set; }

    /// <summary>
    /// Gets or sets what accepting the estimate invoices at once.
    /// </summary>
    public EstimateConversion OnAcceptance { get; set; }

    /// <summary>
    /// Gets or sets who accepted the offer, when, from where, and the terms they were shown.
    /// </summary>
    public EstimateAcceptance Acceptance { get; set; }

    /// <summary>
    /// Gets or sets why the customer declined, when they said.
    /// </summary>
    public string DeclineReason { get; set; }

    /// <summary>
    /// Gets or sets when it was sent, in UTC.
    /// </summary>
    public DateTime? SentUtc { get; set; }

    /// <summary>
    /// Gets or sets when the customer accepted or declined it, or it expired, in UTC.
    /// </summary>
    public DateTime? ClosedUtc { get; set; }

    /// <summary>
    /// Gets or sets the revision, from 1. A revision of a sent estimate is numbered after it, such as <c>EST-1001-2</c>.
    /// </summary>
    public int Revision { get; set; } = 1;

    /// <summary>
    /// Gets or sets the number the revisions of an estimate share, such as <c>EST-1001</c>.
    /// </summary>
    public string BaseNumber { get; set; }

    /// <summary>
    /// Gets or sets the estimate this one revises.
    /// </summary>
    public string RevisionOfId { get; set; }

    /// <summary>
    /// Gets or sets the revision that replaced this estimate.
    /// </summary>
    public string ReplacedById { get; set; }

    /// <summary>
    /// Gets the invoices made from the estimate: a deposit invoice, then the invoice of the work.
    /// </summary>
    public IList<string> InvoiceIds { get; set; } = [];
}

/// <summary>
/// Where an estimate stands.
/// </summary>
public enum EstimateStatus
{
    /// <summary>
    /// Being written; no number yet.
    /// </summary>
    Draft = 0,

    /// <summary>
    /// Numbered and sent; waiting for the customer.
    /// </summary>
    Sent = 1,

    /// <summary>
    /// The customer accepted it.
    /// </summary>
    Accepted = 2,

    /// <summary>
    /// The customer declined it.
    /// </summary>
    Declined = 3,

    /// <summary>
    /// Its expiry date passed before the customer answered.
    /// </summary>
    Expired = 4,

    /// <summary>
    /// Withdrawn, or replaced by a revision.
    /// </summary>
    Canceled = 5,

    /// <summary>
    /// Turned into the invoice of the work.
    /// </summary>
    Invoiced = 6,
}

/// <summary>
/// What turning an estimate into an invoice bills.
/// </summary>
public enum EstimateConversion
{
    /// <summary>
    /// Nothing: staff invoice it later.
    /// </summary>
    None = 0,

    /// <summary>
    /// The whole estimate, as the invoice of the work.
    /// </summary>
    Full = 1,

    /// <summary>
    /// The deposit only, as a prepayment invoice.
    /// </summary>
    Deposit = 2,
}

/// <summary>
/// The record of a customer accepting an estimate.
/// </summary>
public sealed class EstimateAcceptance
{
    /// <summary>
    /// Gets or sets the name the customer typed.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets when they accepted, in UTC.
    /// </summary>
    public DateTime AcceptedUtc { get; set; }

    /// <summary>
    /// Gets or sets the address they accepted from.
    /// </summary>
    public string IpAddress { get; set; }

    /// <summary>
    /// Gets or sets the terms printed on the estimate they accepted.
    /// </summary>
    public string TermsShown { get; set; }

    /// <summary>
    /// Gets or sets the user who accepted, when they were signed in.
    /// </summary>
    public string UserId { get; set; }
}
