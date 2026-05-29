using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Memory.Handlers;

internal sealed class AIMemoryAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMemoryAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public AIMemoryAuthorizationHandler(IServiceProvider serviceProvider)
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

        if (requirement.Permission.Name != AIPermissions.ClearAIMemory.Name)
        {
            return;
        }

        if (context.Resource is not string targetUserId)
        {
            return;
        }

        var currentUserId = context.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (string.IsNullOrEmpty(currentUserId))
        {
            return;
        }

        var isOwnMemory = string.IsNullOrEmpty(targetUserId) ||
            string.Equals(currentUserId, targetUserId, StringComparison.Ordinal);

        // Lazy load to prevent circular dependencies.
        _authorizationService ??= _serviceProvider.GetService<IAuthorizationService>();

        if (isOwnMemory)
        {
            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.ClearAIMemory))
            {
                context.Succeed(requirement);
            }
        }
        else
        {
            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.ClearAIMemoryForOthers))
            {
                context.Succeed(requirement);
            }
        }
    }
}
