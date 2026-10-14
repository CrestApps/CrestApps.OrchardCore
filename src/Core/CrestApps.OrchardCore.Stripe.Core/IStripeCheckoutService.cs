using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Creates Stripe Checkout Sessions for the hosted or embedded Checkout integration.
/// </summary>
public interface IStripeCheckoutService
{
    Task<CreateCheckoutSessionResponse> CreateAsync(CreateCheckoutSessionRequest request);

    /// <summary>
    /// Retrieves an existing Checkout Session so a purchase can be finalized after the customer
    /// returns from a hosted checkout. Returns <see langword="null"/> when the session cannot be found.
    /// </summary>
    Task<CheckoutSessionDetails> GetAsync(string checkoutSessionId);
}
