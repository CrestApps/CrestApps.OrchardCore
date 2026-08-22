using CrestApps.OrchardCore.Stripe.Core;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Subscriptions.ViewModels;

/// <summary>
/// Represents the Stripe client configuration used by a subscription payment method step.
/// </summary>
public class StripePaymentMethodViewModel
{
    /// <summary>
    /// Gets or sets the subscription payment session identifier.
    /// </summary>
    [BindNever]
    public string SessionId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether Stripe live mode is active.
    /// </summary>
    [BindNever]
    public bool IsLive { get; set; }

    /// <summary>
    /// Gets or sets the Stripe publishable key used by the browser client.
    /// </summary>
    [BindNever]
    public string PublishableKey { get; set; }

    /// <summary>
    /// Gets or sets the Stripe checkout mode used for the payment session.
    /// </summary>
    [BindNever]
    public StripeCheckoutMode CheckoutMode { get; set; }
}
