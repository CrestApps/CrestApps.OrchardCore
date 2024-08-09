using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

public interface IStripeSetupIntentService
{
    Task<CreateSetupIntentResponse> CreateAsync(CreateSetupIntentRequest model);
}
