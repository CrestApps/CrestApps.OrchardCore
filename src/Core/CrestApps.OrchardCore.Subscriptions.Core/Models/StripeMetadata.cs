
using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Subscriptions.Core.Models;

/// <summary>
/// Stores Stripe identifiers and subscription metadata for a subscription session.
/// </summary>
public class StripeMetadata
{
    /// <summary>
    /// Gets or sets the Stripe customer identifier.
    /// </summary>
    public string CustomerId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe payment method identifier.
    /// </summary>
    public string PaymentMethodId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe SetupIntent identifier used to collect a reusable payment method.
    /// </summary>
    public string SetupIntentId { get; set; }

    /// <summary>
    /// Gets or sets the Stripe PaymentIntent identifier used for an initial payment.
    /// </summary>
    public string PaymentIntentId { get; set; }

    /// <summary>
    /// Gets or sets Stripe subscription metadata keyed by Stripe subscription identifier.
    /// </summary>
    public Dictionary<string, StripeSubscriptionMetadata> Subscriptions { get; set; } = [];
}

/// <summary>
/// Stores metadata for a Stripe subscription created during a subscription session.
/// </summary>
public class StripeSubscriptionMetadata
{
    /// <summary>
    /// Gets or sets the Stripe subscription identifier.
    /// </summary>
    public string SubscriptionId { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the Stripe subscription was created.
    /// </summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the UTC time at which the current subscription period expires.
    /// </summary>
    public DateTime? ExpiresAt { get; set; }

    /// <summary>
    /// Gets or sets the Stripe subscription line items associated with the subscription.
    /// </summary>
    public IList<CreateSubscriptionLineItem> LineItems { get; set; }
}
