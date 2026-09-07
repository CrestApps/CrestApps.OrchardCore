using CrestApps.OrchardCore.Subscriptions.Core;
using CrestApps.OrchardCore.Subscriptions.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using OrchardCore.Users;
using OrchardCore.Users.Services;

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
/// money quietly and indefinitely.
/// </remarks>
public sealed class RoleSubscriptionEntitlementApplier : ISubscriptionEntitlementApplier
{
    private readonly UserManager<IUser> _userManager;
    private readonly IUserService _userService;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleSubscriptionEntitlementApplier"/> class.
    /// </summary>
    /// <param name="userManager">The user manager used to add and remove the role.</param>
    /// <param name="userService">The user service used to resolve the subscription's owner.</param>
    /// <param name="logger">The logger.</param>
    public RoleSubscriptionEntitlementApplier(
        UserManager<IUser> userManager,
        IUserService userService,
        ILogger<RoleSubscriptionEntitlementApplier> logger)
    {
        _userManager = userManager;
        _userService = userService;
        _logger = logger;
    }

    /// <inheritdoc/>
    public string Kind => SubscriptionConstants.EntitlementKinds.Role;

    /// <inheritdoc/>
    public async Task ApplyAsync(SubscriptionEntitlementContext context)
    {
        var (user, roleName) = await ResolveAsync(context);

        if (user is null || string.IsNullOrEmpty(roleName))
        {
            return;
        }

        if (await _userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var result = await _userManager.AddToRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            _logger.LogError(
                "Could not add the '{RoleName}' role for subscription '{SubscriptionId}': {Errors}",
                roleName,
                context.Subscription.ItemId,
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    /// <inheritdoc/>
    public async Task RevokeAsync(SubscriptionEntitlementContext context)
    {
        var (user, roleName) = await ResolveAsync(context);

        if (user is null || string.IsNullOrEmpty(roleName))
        {
            return;
        }

        if (!await _userManager.IsInRoleAsync(user, roleName))
        {
            return;
        }

        var result = await _userManager.RemoveFromRoleAsync(user, roleName);

        if (!result.Succeeded)
        {
            _logger.LogError(
                "Could not remove the '{RoleName}' role for subscription '{SubscriptionId}': {Errors}",
                roleName,
                context.Subscription.ItemId,
                string.Join("; ", result.Errors.Select(error => error.Description)));
        }
    }

    private async Task<(IUser User, string RoleName)> ResolveAsync(SubscriptionEntitlementContext context)
    {
        var roleName = context?.Entitlement?.Value;
        var ownerId = context?.Subscription?.OwnerId;

        if (string.IsNullOrEmpty(roleName) || string.IsNullOrEmpty(ownerId))
        {
            return (null, null);
        }

        // A guest subscriber has no account to put in a role. The subscription is still valid; it simply
        // cannot grant a role until the buyer has an account.
        var user = await _userService.GetUserByUniqueIdAsync(ownerId);

        return (user, roleName);
    }
}
