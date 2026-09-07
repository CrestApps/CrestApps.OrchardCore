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

    /// <summary>
    /// Reads the current state of a Stripe subscription.
    /// </summary>
    /// <param name="subscriptionId">The Stripe subscription identifier.</param>
    /// <returns>The subscription's authoritative state, or <see langword="null"/> when it does not exist.</returns>
    Task<SubscriptionDetails> GetAsync(string subscriptionId);

    /// <summary>
    /// Stops billing a Stripe subscription.
    /// </summary>
    /// <param name="model">The cancellation request.</param>
    /// <returns>The subscription's state after the cancellation.</returns>
    Task<SubscriptionDetails> CancelAsync(CancelSubscriptionRequest model);

    /// <summary>
    /// Changes what a Stripe subscription bills.
    /// </summary>
    /// <param name="model">The change request.</param>
    /// <returns>The subscription's state after the change.</returns>
    Task<SubscriptionDetails> UpdateAsync(UpdateSubscriptionRequest model);
}
