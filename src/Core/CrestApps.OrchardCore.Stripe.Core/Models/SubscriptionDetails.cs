namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// The authoritative state of a Stripe subscription, read back from Stripe rather than inferred from a
/// webhook. Verification of a recurring obligation depends on this, so a subscription is never treated as
/// paid when the first invoice actually failed.
/// </summary>
public class SubscriptionDetails
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string Id { get; set; }

    /// <summary>
    /// Gets or sets the raw Stripe status, for example <c>active</c>, <c>trialing</c>, or <c>incomplete</c>.
    /// </summary>
    public string Status { get; set; }

    /// <summary>
    /// Gets or sets the ISO-4217 currency the subscription bills in.
    /// </summary>
    public string Currency { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription lives in Stripe's live mode.
    /// </summary>
    public bool LiveMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the subscription is set to end when the paid period does.
    /// </summary>
    public bool CancelAtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the UTC end of the period the customer has already paid for.
    /// </summary>
    public DateTime? CurrentPeriodEndUtc { get; set; }

    /// <summary>
    /// Gets or sets the UTC time the subscription was canceled, when it was.
    /// </summary>
    public DateTime? CanceledAtUtc { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the payment for the latest invoice, used as the transaction id when
    /// the first cycle settles.
    /// </summary>
    public string LatestPaymentId { get; set; }

    /// <summary>
    /// Gets or sets the total actually paid on the latest invoice, in major currency units.
    /// </summary>
    public decimal AmountPaid { get; set; }

    /// <summary>
    /// Gets or sets the client secret the browser confirms when the first invoice needs customer action.
    /// </summary>
    public string ClientSecret { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the latest invoice has been paid in full.
    /// </summary>
    public bool LatestInvoicePaid { get; set; }
}
