using System.ComponentModel.DataAnnotations;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Determines which Stripe integration model is used to collect payment during a checkout.
/// </summary>
public enum StripeCheckoutMode
{
    /// <summary>
    /// Collect payment on-site using Stripe Elements together with Payment/Setup Intents that are
    /// confirmed in the browser. This is the original ("legacy") integration.
    /// </summary>
    [Display(Name = "Payment Elements (on-site)")]
    PaymentElements = 0,

    /// <summary>
    /// Redirect the customer to a Stripe-hosted Checkout page created from a Checkout Session.
    /// This is the integration Stripe currently recommends.
    /// </summary>
    [Display(Name = "Hosted Checkout (redirect)")]
    HostedCheckout = 1,
}
