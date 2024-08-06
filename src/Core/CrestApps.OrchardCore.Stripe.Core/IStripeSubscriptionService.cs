using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripeSubscriptionService
{
    Task<CreateSubscriptionResponse> CreateAsync(CreateSubscriptionRequest model);
}
