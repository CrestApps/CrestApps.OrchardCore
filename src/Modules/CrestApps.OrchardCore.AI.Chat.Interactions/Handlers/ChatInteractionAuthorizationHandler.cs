using System.Security.Claims;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Chat.Interactions.Handlers;

internal sealed class ChatInteractionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public ChatInteractionAuthorizationHandler(IServiceProvider serviceProvider)
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

        if (context.Resource is not ChatInteraction interaction)
        {
            return;
        }

        if (requirement.Permission == AIPermissions.EditChatInteractions)
        {
            // Lazy load to prevent circular dependencies
            _authorizationService ??= _serviceProvider.GetService<IAuthorizationService>();

            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.EditOwnChatInteractions) &&
                context.User.FindFirstValue(ClaimTypes.NameIdentifier) == interaction.OwnerId)
            {
                context.Succeed(requirement);

                return;
            }

            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.EditChatInteractions))
            {
                context.Succeed(requirement);

                return;
            }
        }
        else if (requirement.Permission == AIPermissions.DeleteChatInteraction)
        {
            // Lazy load to prevent circular dependencies
            _authorizationService ??= _serviceProvider.GetService<IAuthorizationService>();

            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.DeleteOwnChatInteraction) && context.User.FindFirstValue(ClaimTypes.NameIdentifier) == interaction.OwnerId)
            {
                context.Succeed(requirement);

                return;
            }

            if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.DeleteChatInteraction))
            {
                context.Succeed(requirement);

                return;
            }
        }
    }
}
