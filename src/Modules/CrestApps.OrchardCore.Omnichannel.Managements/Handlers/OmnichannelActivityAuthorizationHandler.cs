using System.Security.Claims;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Handlers;

internal sealed class OmnichannelActivityAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly Lazy<IAuthorizationService> _authorizationService;

    /// <param name="authorizationService">
    /// The authorization service, resolved lazily: an authorization handler that constructed it eagerly would
    /// close the cycle between the service and the handlers it runs.
    /// </param>
    public OmnichannelActivityAuthorizationHandler(Lazy<IAuthorizationService> authorizationService)
    {
        _authorizationService = authorizationService;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded)
        {
            return;
        }

        if (requirement.Permission != OmnichannelConstants.Permissions.CompleteActivity)
        {
            return;
        }

        var activity = GetActivity(context.Resource);

        if (activity is null || !OwnsActivity(context.User, activity))
        {
            return;
        }

        if (await _authorizationService.Value.AuthorizeAsync(context.User, OmnichannelConstants.Permissions.CompleteOwnActivity))
        {
            context.Succeed(requirement);
        }
    }

    private static OmnichannelActivity GetActivity(object resource)
    {
        if (resource is OmnichannelActivity activity)
        {
            return activity;
        }

        if (resource is OmnichannelActivityContainer container)
        {
            return container.Activity;
        }

        return null;
    }

    private static bool OwnsActivity(ClaimsPrincipal user, OmnichannelActivity activity)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(userId))
        {
            return false;
        }

        return string.Equals(activity.AssignedToId, userId, StringComparison.Ordinal) ||
            string.Equals(activity.ReservedById, userId, StringComparison.Ordinal);
    }
}
