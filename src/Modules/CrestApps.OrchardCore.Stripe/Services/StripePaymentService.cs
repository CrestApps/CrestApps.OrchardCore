using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripePaymentService : IStripePaymentService
{
    private readonly PaymentIntentService _paymentIntentService;

    public StripePaymentService(StripeClient stripeClient)
    {
        _paymentIntentService = new PaymentIntentService(stripeClient);
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
            ClientSecret = paymentIntent.ClientSecret,
            CustomerId = paymentIntent.CustomerId,
            Status = paymentIntent.Status,
        };
    }
}
