using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripePaymentIntentService : IStripePaymentIntentService
{
    private readonly PaymentIntentService _paymentIntentService;

    public StripePaymentIntentService(StripeClient stripeClient)
    {
        _paymentIntentService = new PaymentIntentService(stripeClient);
    }

    public async Task<ConfirmPaymentIntentResponse> ConfirmAsync(ConfirmPaymentIntentRequest model)
    {
        var confirmOptions = new PaymentIntentConfirmOptions();

        if (!string.IsNullOrEmpty(model.PaymentMethodId))
        {
            confirmOptions.PaymentMethod = model.PaymentMethodId;
        }

        var result = await _paymentIntentService.ConfirmAsync(model.PaymentIntentId, confirmOptions);

        return new ConfirmPaymentIntentResponse
        {
            Status = result.Status,
            Id = result.Id,
            Amount = result.Amount,
            Currency = result.Currency,
            CustomerId = result.Customer?.Id,
        };
    }

    public async Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var paymentIntentOptions = new PaymentIntentCreateOptions
        {
            Amount = (int)(model.Amount * 100), // Amount in cents (e.g., 1000 equals $10.00)
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

        var paymentIntent = await _paymentIntentService.CreateAsync(paymentIntentOptions);

        return new CreatePaymentIntentResponse()
        {
            Id = paymentIntent.Id,
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }
}
