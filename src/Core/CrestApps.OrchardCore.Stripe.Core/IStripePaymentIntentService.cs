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

    /// <summary>
    /// Creates an unconfirmed Stripe payment intent for a generic checkout payment collected through
    /// embedded Stripe Elements, returning the client secret the browser uses to confirm it.
    /// </summary>
    /// <param name="model">The details of the checkout payment intent to create.</param>
    /// <returns>The result of the create operation, including the client secret.</returns>
    Task<CreatePaymentIntentResponse> CreateForCheckoutAsync(CreateCheckoutPaymentIntentRequest model);

    /// <summary>
    /// Retrieves the authoritative state of a Stripe payment intent so a checkout can verify what really
    /// happened at the gateway rather than trusting a cached notification.
    /// </summary>
    /// <param name="model">The retrieve request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details.</returns>
    Task<PaymentIntentDetails> RetrieveAsync(RetrievePaymentIntentRequest model);

    /// <summary>
    /// Cancels a Stripe payment intent that has not yet been captured, releasing the remote resource for an
    /// abandoned or compensated checkout attempt.
    /// </summary>
    /// <param name="model">The cancel request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details after cancellation.</returns>
    Task<PaymentIntentDetails> CancelAsync(CancelPaymentIntentRequest model);
}
