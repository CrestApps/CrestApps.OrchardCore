using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Users;

namespace CrestApps.OrchardCore.Subscriptions.Services;

/// <summary>
/// Grants and removes an Orchard Core role for as long as a subscription is current.
/// </summary>
/// <remarks>
/// This is what turns "member-only access" from a plan description into something the site enforces. Roles
/// already gate content, features, and permissions across Orchard Core, so putting the subscriber in a role
/// makes every one of those gates subscription-aware without any of them knowing subscriptions exist.
///
/// Removing the role when access ends matters as much as granting it. A subscriber who stops paying and
/// keeps the role keeps everything they were paying for, which is the failure that costs the site owner
/// money quietly and indefinitely. The grant goes through <see cref="IRoleGrantService"/>, keyed by the
/// subscription, so the role is removed only when nothing else, such as an active membership, still grants it.
/// </remarks>
public sealed class RoleSubscriptionEntitlementApplier : ISubscriptionEntitlementApplier
{
    /// <summary>
    /// The source name that subscriptions' role grants are recorded under.
    /// </summary>
    public const string SourceName = "Subscription";

    private readonly IRoleGrantService _roleGrantService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleSubscriptionEntitlementApplier"/> class.
    /// </summary>
    /// <param name="roleGrantService">The service that records the grant and adds or removes the role.</param>
    public RoleSubscriptionEntitlementApplier(IRoleGrantService roleGrantService)
    {
        _roleGrantService = roleGrantService;
    }

    /// <inheritdoc/>
    public string Kind => SubscriptionConstants.EntitlementKinds.Role;

    /// <inheritdoc/>
    public async Task ApplyAsync(SubscriptionEntitlementContext context)
    {
        if (TryResolve(context, out var ownerId, out var roleName))
        {
            // A guest subscriber has no account to put in a role. The subscription is still valid; it simply
            // cannot grant a role until the buyer has an account.
            await _roleGrantService.GrantAsync(ownerId, roleName, SourceName, context.Subscription.ItemId);
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(SubscriptionEntitlementContext context)
    {
        if (TryResolve(context, out var ownerId, out var roleName))
        {
            await _roleGrantService.RevokeAsync(ownerId, roleName, SourceName, context.Subscription.ItemId);
        }
    }

    private static bool TryResolve(SubscriptionEntitlementContext context, out string ownerId, out string roleName)
    {
        roleName = context?.Entitlement?.Value;
        ownerId = context?.Subscription?.OwnerId;

        return !string.IsNullOrEmpty(roleName) && !string.IsNullOrEmpty(ownerId);
    }
}
