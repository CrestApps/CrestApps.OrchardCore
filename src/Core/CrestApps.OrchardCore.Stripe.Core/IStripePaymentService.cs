using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePaymentService
{
    Task<CreatePaymentIntentResponse> CreateAsync(CreatePaymentIntentRequest model);

    Task<ConfirmPaymentIntentResponse> ConfirmAsync(ConfirmPaymentIntentRequest model);
}
