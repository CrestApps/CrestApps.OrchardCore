using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Users;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Reports the roles a user's current subscriptions granted before grants were recorded by source, so ending another
/// grant of the same role does not take it away from a subscriber.
/// </summary>
public sealed class SubscriptionRoleGrantSource : IRoleGrantSource
{
    private readonly ISubscriptionStore _subscriptionStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionRoleGrantSource"/> class.
    /// </summary>
    /// <param name="subscriptionStore">The store of subscriptions.</param>
    /// <param name="clock">The clock used to decide whether a subscription is current.</param>
    public SubscriptionRoleGrantSource(ISubscriptionStore subscriptionStore, IClock clock)
    {
        _subscriptionStore = subscriptionStore;
        _clock = clock;
    }

    /// <inheritdoc/>
    public string Source => RoleSubscriptionEntitlementApplier.SourceName;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ExistingRoleGrant>> ListExistingGrantsAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return [];
        }

        var now = _clock.UtcNow;

        // Subscriptions used to take their role away when they ended, whoever else granted it, so their roles count as
        // added by them.
        return [.. (await _subscriptionStore.GetByOwnerAsync(userId))
            .Where(subscription => subscription.IsCurrent(now))
            .SelectMany(subscription => subscription.Entitlements
                .Where(entitlement => string.Equals(entitlement.Kind, SubscriptionConstants.EntitlementKinds.Role, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(entitlement.Value))
                .Select(entitlement => new ExistingRoleGrant
                {
                    RoleName = entitlement.Value,
                    SourceId = subscription.ItemId,
                    RoleAddedBySource = true,
                }))];
    }
}
