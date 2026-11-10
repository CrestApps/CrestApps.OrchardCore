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
    /// Charges a payment method the customer saved earlier, server-side and without the customer present. A
    /// declined card is reported in the response rather than thrown, so the caller can record why.
    /// </summary>
    /// <param name="model">The charge to make.</param>
    /// <returns>Whether Stripe accepted the charge, and why not when it did not.</returns>
    Task<ChargeOffSessionResponse> ChargeOffSessionAsync(ChargeOffSessionRequest model);

    /// <summary>
    /// Creates and confirms a PaymentIntent with a payment credential an AI agent brought, on the server.
    /// </summary>
    /// <param name="model">What to charge and the credential.</param>
    Task<ChargeDelegatedCredentialResponse> ChargeDelegatedAsync(ChargeDelegatedCredentialRequest model);

    /// <summary>
    /// Confirms again a PaymentIntent made from a credential an AI agent brought, with the result of a 3-D Secure
    /// authentication the agent ran, so the same payment goes ahead without Stripe asking the buyer again.
    /// </summary>
    /// <param name="model">The PaymentIntent and the 3-D Secure result.</param>
    Task<ChargeDelegatedCredentialResponse> ConfirmDelegatedAsync(ConfirmDelegatedCredentialRequest model);

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

    /// <summary>
    /// Finds the PaymentIntent made for a checkout attempt, from its <c>checkout_attempt_id</c> metadata. A charge whose
    /// answer never arrived (a Stripe server error, a timeout) may still have been made, and this is how the store finds
    /// it. Stripe's search can lag up to a minute behind a new PaymentIntent.
    /// </summary>
    /// <param name="checkoutAttemptId">The checkout attempt's id.</param>
    /// <returns>The PaymentIntent's id, or <see langword="null"/> when none is found.</returns>
    Task<string> FindByCheckoutAttemptAsync(string checkoutAttemptId)
        => Task.FromResult<string>(null);
}
