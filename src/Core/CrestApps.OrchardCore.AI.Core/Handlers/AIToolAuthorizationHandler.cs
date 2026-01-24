using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Core.Handlers;

/// <summary>
/// Authorization handler for AI tool access control.
/// Handles Permission #2 (AccessAITool) and maps it to Permission #3 (per-tool permissions).
/// </summary>
public sealed class AIToolAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public AIToolAuthorizationHandler(IServiceProvider serviceProvider)
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

        // Resource must be provided (AI tool name or instance ID)
        if (context.Resource is null)
        {
            return;
        }

        // Only handle AccessAITool permission (Permission #2)
        if (requirement.Permission != AIPermissions.AccessAITool)
        {
            return;
        }

        var toolIdentifier = GetToolIdentifier(context.Resource);

        if (string.IsNullOrEmpty(toolIdentifier))
        {
            return;
        }

        _authorizationService ??= _serviceProvider.GetService<IAuthorizationService>();

        // Check the specific tool permission (Permission #3)
        // Note: AccessAnyAITool is implied by the permission hierarchy and will be automatically checked
        var toolPermission = AIPermissions.CreateAIToolPermission(toolIdentifier);

        if (await _authorizationService.AuthorizeAsync(context.User, toolPermission))
        {
            context.Succeed(requirement);
        }
    }

    private static string GetToolIdentifier(object resource)
    {
        // Resource can be a string (tool name or instance ID)
        if (resource is string toolIdentifier)
        {
            return toolIdentifier;
        }

        if (resource is AIToolDefinitionEntry entry)
        {
            return entry.Name;
        }

        if (resource is AIToolInstance aIToolInstance)
        {
            return aIToolInstance.ItemId;
        }

        // Resource can also be an AITool instance
        if (resource is AITool aiTool)
        {
            return aiTool.Name;
        }

        return null;
    }
}
