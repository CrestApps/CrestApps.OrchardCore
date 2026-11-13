namespace CrestApps.OrchardCore.Stripe.Core.Models;

/// <summary>
/// Represents a request to stop billing a Stripe subscription.
/// </summary>
public sealed class CancelSubscriptionRequest : StripeWriteRequest
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether billing stops at the end of the period the customer has
    /// already paid for rather than immediately. Ending it immediately takes away access the customer paid
    /// for, so this is the usual choice.
    /// </summary>
    public bool AtPeriodEnd { get; set; }

    /// <summary>
    /// Gets or sets the reason recorded with Stripe's cancellation feedback.
    /// </summary>
    public string Reason { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether an immediate cancellation also voids the subscription's latest invoice
    /// while it is open. Stripe only stops collecting an open invoice when its subscription is canceled; the buyer could
    /// still pay it with its client secret.
    /// </summary>
    public bool VoidOpenInvoice { get; set; }
}
