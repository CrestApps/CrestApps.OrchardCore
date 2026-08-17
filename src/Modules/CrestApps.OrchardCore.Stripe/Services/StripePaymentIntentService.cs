using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Implements Stripe PaymentIntent operations.
/// </summary>
public sealed class StripePaymentIntentService : IStripePaymentIntentService
{
    private readonly PaymentIntentService _paymentIntentService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripePaymentIntentService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to call the Stripe API.</param>
    public StripePaymentIntentService(StripeClient stripeClient)
    {
        _paymentIntentService = new PaymentIntentService(stripeClient);
    }

    /// <summary>
    /// Confirms an existing Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The payment intent confirmation request.</param>
    /// <returns>The confirmed payment intent details returned by Stripe.</returns>
    public async Task<ConfirmPaymentIntentResponse> ConfirmAsync(ConfirmPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        var confirmOptions = new PaymentIntentConfirmOptions();

        if (!string.IsNullOrEmpty(model.PaymentMethodId))
        {
            confirmOptions.PaymentMethod = model.PaymentMethodId;
        }

        PaymentIntent result;

        try
        {
            result = await _paymentIntentService.ConfirmAsync(model.PaymentIntentId, confirmOptions, model.ToRequestOptions());
        }
        catch (StripeException ex) when (ex.StripeError?.Code == "payment_intent_unexpected_state")
        {
            // Confirmation must be idempotent. By the time this runs, the PaymentIntent may already
            // have reached a terminal state through client-side confirmation (Payment Elements),
            // Stripe's automatic subscription collection, or an earlier at-least-once webhook delivery.
            // Stripe rejects re-confirming such an intent with 'payment_intent_unexpected_state'. Treat
            // that as success by returning the authoritative current state instead of throwing, which
            // would fail the webhook and make Stripe retry a permanently failing request forever.
            result = await _paymentIntentService.GetAsync(model.PaymentIntentId, options: null, model.ToRequestOptions());
        }

        return new ConfirmPaymentIntentResponse
        {
            Status = result.Status,
            Id = result.Id,
            Amount = result.Amount,
            Currency = result.Currency,
            CustomerId = result.Customer?.Id,
        };
    }

    /// <summary>
    /// Creates and confirms a Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The payment intent creation request.</param>
    /// <returns>The created payment intent details returned by Stripe.</returns>
    public async Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = StripeCurrency.ToMinorUnits(model.Amount ?? 0, model.Currency), // Currency-aware minor units (e.g. 1000 == $10.00, 500 == ¥500).
            Currency = model.Currency,
            PaymentMethod = model.PaymentMethodId,
            Customer = model.CustomerId,
            Confirm = true,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never"
            },
            Metadata = model.Metadata,
        };

        var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, model.ToRequestOptions());

        return new CreatePaymentIntentResponse()
        {
            Id = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }

    /// <summary>
    /// Creates an unconfirmed Stripe PaymentIntent for a generic checkout payment collected through
    /// embedded Stripe Elements.
    /// </summary>
    /// <param name="model">The checkout payment intent creation request.</param>
    /// <returns>The created payment intent details, including the client secret used to confirm it.</returns>
    public async Task<CreatePaymentIntentResponse> CreateForCheckoutAsync(CreateCheckoutPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            // Currency-aware minor units (for example 1000 == $10.00, 500 == ¥500).
            Amount = StripeCurrency.ToMinorUnits(model.Amount, model.Currency),
            Currency = model.Currency,
            Customer = model.CustomerId,
            Description = model.Description,

            // The customer confirms with Stripe.js using the returned client secret, so the payment goes
            // through Strong Customer Authentication instead of being confirmed off-session.
            Confirm = false,
            AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
            {
                Enabled = true,
                AllowRedirects = "never",
            },
            Metadata = model.Metadata,
        };

        var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions, model.ToRequestOptions());

        return new CreatePaymentIntentResponse
        {
            Id = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }

    /// <summary>
    /// Retrieves the authoritative state of a Stripe PaymentIntent.
    /// </summary>
    /// <param name="model">The retrieve request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details.</returns>
    public async Task<PaymentIntentDetails> RetrieveAsync(RetrievePaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        var paymentIntent = await _paymentIntentService.GetAsync(model.PaymentIntentId);

        return ToDetails(paymentIntent);
    }

    /// <summary>
    /// Cancels a Stripe PaymentIntent that has not yet been captured.
    /// </summary>
    /// <param name="model">The cancel request identifying the payment intent.</param>
    /// <returns>The authoritative payment intent details after cancellation.</returns>
    public async Task<PaymentIntentDetails> CancelAsync(CancelPaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);
        ArgumentException.ThrowIfNullOrEmpty(model.PaymentIntentId);

        var cancelOptions = new PaymentIntentCancelOptions();

        if (!string.IsNullOrEmpty(model.CancellationReason))
        {
            cancelOptions.CancellationReason = model.CancellationReason;
        }

        var paymentIntent = await _paymentIntentService.CancelAsync(model.PaymentIntentId, cancelOptions, model.ToRequestOptions());

        return ToDetails(paymentIntent);
    }

    private static PaymentIntentDetails ToDetails(PaymentIntent paymentIntent)
        => new()
        {
            Id = paymentIntent.Id,
            Status = paymentIntent.Status,
            Amount = paymentIntent.Amount,
            AmountReceived = paymentIntent.AmountReceived,
            Currency = paymentIntent.Currency,
            LiveMode = paymentIntent.Livemode,
            LatestChargeId = paymentIntent.LatestChargeId,
        };
}
