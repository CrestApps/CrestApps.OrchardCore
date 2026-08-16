using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

/// <summary>
/// Grants subscription management permission to users who own a subscription and can manage their own subscriptions.
/// </summary>
public sealed class SubscriptionsPermissionsHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="SubscriptionsPermissionsHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider used to resolve authorization services lazily.</param>
    public SubscriptionsPermissionsHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Evaluates whether the current user can manage the subscription session in the authorization resource.
    /// </summary>
    /// <param name="context">The authorization context that contains the user and resource.</param>
    /// <param name="requirement">The permission requirement being evaluated.</param>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded)
        {
            // This handler is not revoking any pre-existing grants.
            return;
        }
        var subscription = context.Resource as SubscriptionSession;

        if (context.Resource == null ||
            requirement.Permission != SubscriptionPermissions.ManageSubscriptions)
        {
            return;
        }

        // Lazy load to prevent circular dependencies.
        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (IsOwner(context.User, subscription) &&
            await _authorizationService.AuthorizeAsync(context.User, SubscriptionPermissions.ManageOwnSubscriptions))
        {
            context.Succeed(requirement);

            return;
        }
    }

    private static bool IsOwner(ClaimsPrincipal user, SubscriptionSession subscription)
    {
        if (user == null || subscription == null)
        {
            return false;
        }

        return user.FindFirstValue(ClaimTypes.NameIdentifier) == subscription.OwnerId;
    }
}
