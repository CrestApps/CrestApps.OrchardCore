using CrestApps.AI;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

public sealed class OrchardAIChatSessionDocumentAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, AIChatSessionDocumentAuthorizationContext>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    public OrchardAIChatSessionDocumentAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AIChatSessionDocumentAuthorizationContext resource)
    {
        if (context.HasSucceeded ||
            requirement.Name != AIChatDocumentOperations.ManageDocuments.Name ||
            resource == null)
        {
            return;
        }

        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (!await _authorizationService.AuthorizeAsync(context.User, AIPermissions.QueryAnyAIProfile))
        {
            return;
        }

        if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.QueryAnyAIProfile, resource.Profile))
        {
            context.Succeed(requirement);
        }
    }
}
