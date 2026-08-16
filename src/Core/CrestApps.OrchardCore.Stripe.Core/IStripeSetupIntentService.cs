using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe SetupIntent API for collecting payment details for future payments.
/// </summary>
public interface IStripeSetupIntentService
{
    /// <summary>
    /// Creates a new Stripe setup intent.
    /// </summary>
    /// <param name="model">The details of the setup intent to create.</param>
    /// <returns>The result of the create operation.</returns>
    Task<CreateSetupIntentResponse> CreateAsync(CreateSetupIntentRequest model);
}
