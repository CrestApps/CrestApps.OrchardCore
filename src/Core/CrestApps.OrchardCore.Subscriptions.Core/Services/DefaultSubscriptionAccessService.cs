using CrestApps.OrchardCore.Subscriptions.Models;
using CrestApps.OrchardCore.Subscriptions.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Core.Services;

/// <summary>
/// The default <see cref="ISubscriptionAccessService"/>. It reads the durable agreements a user owns and
/// asks each one whether it is current, rather than filtering on status.
/// </summary>
public sealed class DefaultSubscriptionAccessService : ISubscriptionAccessService
{
    private readonly ISubscriptionManager _subscriptionManager;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSubscriptionAccessService"/> class.
    /// </summary>
    /// <param name="subscriptionManager">The subscription manager.</param>
    /// <param name="clock">The clock used to decide whether an agreement is current.</param>
    public DefaultSubscriptionAccessService(
        ISubscriptionManager subscriptionManager,
        IClock clock)
    {
        _subscriptionManager = subscriptionManager;
        _clock = clock;
    }

    /// <inheritdoc/>
    public async Task<bool> HasEntitlementAsync(string userId, string kind, string value = null, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(kind);

        foreach (var entitlement in await GetEntitlementsAsync(userId, cancellationToken))
        {
            if (!string.Equals(entitlement.Kind, kind, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (value is null || string.Equals(entitlement.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<SubscriptionEntitlement>> GetEntitlementsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var entitlements = new List<SubscriptionEntitlement>();

        foreach (var subscription in await GetCurrentSubscriptionsAsync(userId, cancellationToken))
        {
            entitlements.AddRange(subscription.Entitlements);
        }

        return entitlements;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<Subscription>> GetCurrentSubscriptionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var now = _clock.UtcNow;
        var subscriptions = await _subscriptionManager.GetByOwnerAsync(userId, cancellationToken);

        // The agreement decides whether it is current, not the caller. A cancelled agreement is still owed
        // until the paid period runs out, and a past-due one through its grace window.
        return [.. subscriptions.Where(subscription => subscription.IsCurrent(now))];
    }
}
