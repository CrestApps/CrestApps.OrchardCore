using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripePaymentMethodService
{
    Task<StripePaymentMethodInfoResponse> GetInformationAsync(string paymentMethodId);
}
