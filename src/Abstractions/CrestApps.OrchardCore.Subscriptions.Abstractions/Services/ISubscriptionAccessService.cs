using CrestApps.OrchardCore.Subscriptions.Models;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Answers whether a user currently holds what a subscription grants.
/// </summary>
/// <remarks>
/// This is the question the rest of a site actually asks: may this person read the members-only article,
/// use this feature, run this tenant. Asking it of the subscription's status directly gets it wrong, because
/// status and access are not the same thing. A customer who cancelled mid-cycle keeps what they paid for,
/// and one whose card failed last night keeps it through the grace window. Both of those are people you do
/// not want to lock out.
/// </remarks>
public interface ISubscriptionAccessService
{
    /// <summary>
    /// Returns whether the user holds a current entitlement of the given kind.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="kind">The entitlement kind.</param>
    /// <param name="value">The entitlement value to match, or <see langword="null"/> to match any value of
    /// that kind.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<bool> HasEntitlementAsync(string userId, string kind, string value = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every entitlement the user currently holds.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<SubscriptionEntitlement>> GetEntitlementsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subscriptions the user owns that are current right now.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <param name="cancellationToken">The token to monitor for cancellation requests.</param>
    Task<IReadOnlyList<Subscription>> GetCurrentSubscriptionsAsync(string userId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Applies and revokes one kind of <see cref="SubscriptionEntitlement"/>.
/// </summary>
/// <remarks>
/// The subscription decides whether an entitlement is current; a feature that understands the kind decides
/// what to do about it. Keeping those apart is what lets a role, a tenant, and a feature flag all be
/// granted by a subscription without the subscription module knowing about any of them.
/// </remarks>
public interface ISubscriptionEntitlementApplier
{
    /// <summary>
    /// The entitlement kind this applier handles.
    /// </summary>
    string Kind { get; }

    /// <summary>
    /// Grants the entitlement.
    /// </summary>
    /// <param name="context">The entitlement and the subscription granting it.</param>
    Task ApplyAsync(SubscriptionEntitlementContext context);

    /// <summary>
    /// Takes the entitlement away.
    /// </summary>
    /// <remarks>
    /// This runs when a subscription stops being current, which is later than when it stops billing. A
    /// customer who cancels mid-cycle keeps access until the period they paid for runs out.
    /// </remarks>
    /// <param name="context">The entitlement and the subscription that granted it.</param>
    Task RevokeAsync(SubscriptionEntitlementContext context);
}

/// <summary>
/// The entitlement being applied or revoked, and the subscription it belongs to.
/// </summary>
/// <param name="subscription">The subscription granting the entitlement.</param>
/// <param name="entitlement">The entitlement.</param>
public sealed class SubscriptionEntitlementContext(Subscription subscription, SubscriptionEntitlement entitlement)
{
    /// <summary>
    /// Gets the subscription granting the entitlement.
    /// </summary>
    public Subscription Subscription { get; } = subscription;

    /// <summary>
    /// Gets the entitlement.
    /// </summary>
    public SubscriptionEntitlement Entitlement { get; } = entitlement;
}
