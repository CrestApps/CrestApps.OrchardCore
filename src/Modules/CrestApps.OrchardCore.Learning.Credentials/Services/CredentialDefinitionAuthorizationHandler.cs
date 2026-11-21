using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.Learning.Credentials.Services;

/// <summary>
/// Gives the content permissions on credential definitions to the people who hold Manage credential definitions.
/// </summary>
internal sealed class CredentialDefinitionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private static readonly HashSet<string> _granted = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
        CommonPermissions.EditContent.Name,
        CommonPermissions.EditOwnContent.Name,
        CommonPermissions.PublishContent.Name,
        CommonPermissions.PublishOwnContent.Name,
        CommonPermissions.DeleteContent.Name,
        CommonPermissions.DeleteOwnContent.Name,
        CommonPermissions.CloneContent.Name,
        CommonPermissions.CloneOwnContent.Name,
    };

    private readonly IServiceProvider _services;

    /// <summary>
    /// Initializes a new instance of the <see cref="CredentialDefinitionAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="services">The services. The authorization service is resolved lazily: it depends on the handlers.</param>
    public CredentialDefinitionAuthorizationHandler(IServiceProvider services)
    {
        _services = services;
    }

    /// <inheritdoc/>
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded ||
            context.Resource is not ContentItem { ContentType: CredentialsConstants.ContentTypes.CredentialDefinition } ||
            !_granted.Contains(requirement.Permission.Name) ||
            context.User?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var authorizationService = _services.GetRequiredService<IAuthorizationService>();

        if (await authorizationService.AuthorizeAsync(context.User, LearningCredentialsPermissions.ManageCredentialDefinitions))
        {
            context.Succeed(requirement);
        }
    }
}
