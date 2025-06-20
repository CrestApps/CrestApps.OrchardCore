using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Subscriptions.Core.Handlers;

public sealed class SubscriptionsPermissionsHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public SubscriptionsPermissionsHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

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
