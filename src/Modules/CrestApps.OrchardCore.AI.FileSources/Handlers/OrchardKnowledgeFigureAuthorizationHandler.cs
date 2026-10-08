using CrestApps.Core.AI.Documents;
using CrestApps.Core.AI.Models;
using CrestApps.OrchardCore.AI.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.AI.FileSources.Handlers;

/// <summary>
/// Decides who may see a picture stored in an ingested knowledge base.
/// </summary>
/// <remarks>
/// A figure in a data source is internal knowledge rather than a user's own upload, so there is no owner to
/// check: the rule is a tenant permission. It is deliberately the broad query permission and the manage
/// permission, not a per-profile one -- a figure is reached from an answer, and which profile produced that
/// answer is not knowable from the address. Erring tight here costs a reader a picture; erring loose hands
/// out the contents of an internal knowledge base.
/// <para>
/// Without a handler the framework's figure endpoint refuses every request, and an answer citing a chart
/// shows a broken picture instead.
/// </para>
/// </remarks>
internal sealed class OrchardKnowledgeFigureAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, AIDataSource>
{
    private readonly IServiceProvider _serviceProvider;

    private IAuthorizationService _authorizationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="OrchardKnowledgeFigureAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public OrchardKnowledgeFigureAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement,
        AIDataSource resource)
    {
        if (context.HasSucceeded ||
            requirement.Name != AIKnowledgeOperations.ViewFigures.Name ||
            resource == null)
        {
            return;
        }

        // Resolved lazily rather than injected: an authorization handler that takes the authorization
        // service in its constructor closes a cycle through the container.
        _authorizationService ??= _serviceProvider.GetRequiredService<IAuthorizationService>();

        if (await _authorizationService.AuthorizeAsync(context.User, AIPermissions.QueryAnyAIProfile) ||
            await _authorizationService.AuthorizeAsync(context.User, AIPermissions.ManageAIDataSources))
        {
            context.Succeed(requirement);
        }
    }
}
