using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

public sealed class StripeSetupIntentService : IStripeSetupIntentService
{
    private readonly StripeClient _stripeClient;

    public StripeSetupIntentService(StripeClient stripeClient)
    {
        _stripeClient = stripeClient;
    }

    public async Task<CreateSetupIntentResponse> CreateAsync(CreateSetupIntentRequest model)
    {
        var setupIntentOptions = new SetupIntentCreateOptions
        {
            Customer = model.CustomerId,
            PaymentMethod = model.PaymentMethodId,
            PaymentMethodTypes = ["card"],
            Metadata = model.Metadata,
        };

        var setupIntentService = new SetupIntentService(_stripeClient);

        var setupIntent = await setupIntentService.CreateAsync(setupIntentOptions);

        return new CreateSetupIntentResponse()
        {
            Id = setupIntent.Id,
            Status = setupIntent.Status,
            ClientSecret = setupIntent.ClientSecret,
        };
    }
}
