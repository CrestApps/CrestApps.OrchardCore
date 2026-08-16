using CrestApps.OrchardCore.Stripe.Core;
using CrestApps.OrchardCore.Stripe.Core.Models;
using Stripe;

namespace CrestApps.OrchardCore.Stripe.Services;

/// <summary>
/// Creates Stripe SetupIntent records for saving customer payment methods.
/// </summary>
public sealed class StripeSetupIntentService : IStripeSetupIntentService
{
    private readonly StripeClient _stripeClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="StripeSetupIntentService"/> class.
    /// </summary>
    /// <param name="stripeClient">The Stripe client used to create setup intents.</param>
    public StripeSetupIntentService(StripeClient stripeClient)
    {
        _stripeClient = stripeClient;
    }

    /// <summary>
    /// Creates a Stripe SetupIntent for the requested customer and payment method.
    /// </summary>
    /// <param name="model">The setup intent creation request.</param>
    /// <returns>The created setup intent details, including its client secret.</returns>
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

        var setupIntent = await setupIntentService.CreateAsync(setupIntentOptions, model.ToRequestOptions());

        return new CreateSetupIntentResponse()
        {
            Id = setupIntent.Id,
            Status = setupIntent.Status,
            ClientSecret = setupIntent.ClientSecret,
        };
    }
}
