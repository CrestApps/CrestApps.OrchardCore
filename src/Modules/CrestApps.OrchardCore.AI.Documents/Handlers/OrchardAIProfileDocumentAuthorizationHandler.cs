using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.Documents.Handlers;

/// <summary>
/// Handles events for orchard AI profile document authorization.
/// </summary>
public sealed class OrchardAIProfileDocumentAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, AIProfile>
{
    private readonly IServiceProvider _serviceProvider;
    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardAIProfileDocumentAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public OrchardAIProfileDocumentAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AIProfile resource)
    {
        if (context.HasSucceeded ||
            requirement.Name != AIChatDocumentOperations.ManageDocuments.Name ||
            resource == null)
        {
            return;
        }

        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (!await _authorizationService.AuthorizeAsync(context.User, AIPermissions.ManageAIProfiles))
        {
            return;
        }

        if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.ManageAIProfiles, resource))
        {
            context.Succeed(requirement);
        }
    }
}
