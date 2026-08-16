using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe PaymentIntent API for creating and confirming payment intents.
/// </summary>
public interface IStripePaymentIntentService
{
    /// <summary>
    /// Creates a new Stripe payment intent.
    /// </summary>
    /// <param name="model">The details of the payment intent to create.</param>
    /// <returns>The result of the create operation.</returns>
    Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model);

    /// <summary>
    /// Confirms an existing Stripe payment intent.
    /// </summary>
    /// <param name="model">The details of the payment intent to confirm.</param>
    /// <returns>The result of the confirm operation.</returns>
    Task<ConfirmPaymentIntentResponse> ConfirmAsync(ConfirmPaymentIntentRequest model);
}
