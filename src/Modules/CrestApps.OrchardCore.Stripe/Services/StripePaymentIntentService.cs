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
        var confirmOptions = new PaymentIntentConfirmOptions();

        if (!string.IsNullOrEmpty(model.PaymentMethodId))
        {
            confirmOptions.PaymentMethod = model.PaymentMethodId;
        }

        var result = await _paymentIntentService.ConfirmAsync(model.PaymentIntentId, confirmOptions, model.ToRequestOptions());

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
}
