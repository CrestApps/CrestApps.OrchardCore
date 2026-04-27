using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Handles events for orchard chat interaction document authorization.
/// </summary>
public sealed class OrchardChatInteractionDocumentAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, ChatInteraction>
{
    private readonly IServiceProvider _serviceProvider;
    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardChatInteractionDocumentAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public OrchardChatInteractionDocumentAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        ChatInteraction resource)
    {
        if (context.HasSucceeded ||
            requirement.Name != AIChatDocumentOperations.ManageDocuments.Name ||
            resource == null)
        {
            return;
        }

        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (!await _authorizationService.AuthorizeAsync(context.User, AIPermissions.EditChatInteractions))
        {
            return;
        }

        if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.EditChatInteractions, resource))
        {
            context.Succeed(requirement);
        }
    }
}
