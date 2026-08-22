using CrestApps.OrchardCore.Stripe.Core.Models;

namespace CrestApps.OrchardCore.Stripe.Core;

/// <summary>
/// Provides access to the Stripe Subscription API for creating subscriptions.
/// </summary>
public interface IStripeSubscriptionService
{
    /// <summary>
    /// Creates a new Stripe subscription.
    /// </summary>
    /// <param name="model">The details of the subscription to create.</param>
    /// <returns>The result of the create operation.</returns>
    Task<CreateSubscriptionResponse> CreateAsync(CreateSubscriptionRequest model);
}
