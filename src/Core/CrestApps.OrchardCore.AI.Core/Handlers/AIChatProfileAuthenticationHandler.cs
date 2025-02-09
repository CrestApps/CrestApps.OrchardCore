using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

public sealed class AIChatProfileAuthenticationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public AIChatProfileAuthenticationHandler(IServiceProvider serviceProvider)
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

        if (context.Resource is null)
        {
            return;
        }

        if (requirement.Permission != AIChatPermissions.QueryAnyAIChatProfile)
        {
            return;
        }

        var profileName = GetProfileName(context.Resource);

        if (string.IsNullOrEmpty(profileName))
        {
            return;
        }

        var permission = AIChatPermissions.CreateDynamicPermission(profileName);

        _authorizationService ??= _serviceProvider.GetService<IAuthorizationService>();

        if (await _authorizationService.AuthorizeAsync(context.User, permission))
        {
            context.Succeed(requirement);
        }
    }

    private static string GetProfileName(object resource)
    {
        var profile = resource as AIChatProfile;

        if (profile is not null)
        {
            return profile.Name;
        }

        return resource as string;
    }
}
