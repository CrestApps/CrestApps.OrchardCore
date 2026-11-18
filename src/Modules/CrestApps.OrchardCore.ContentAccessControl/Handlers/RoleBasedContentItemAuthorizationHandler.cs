using CrestApps.OrchardCore.ContentAccessControl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContentAccessControl.Handlers;

/// <summary>
/// Handles events for role based content item authorization.
/// </summary>
public sealed class RoleBasedContentItemAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="RoleBasedContentItemAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    public RoleBasedContentItemAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // It runs even when the View content permission was already granted, as it is to everyone, anonymous visitors
        // included: a restricted item is refused to whoever isn't in its roles, whatever else allowed it.
        var user = context?.User;

        if (user == null || user.IsInRole(OrchardCoreConstants.Roles.Administrator))
        {
            return;
        }

        if (context.Resource == null || requirement.Permission != CommonPermissions.ViewContent)
        {
            return;
        }

        var contentItem = context.Resource as ContentItem;

        if (contentItem == null)
        {
            return;
        }

        var contentDefinitionManager = _serviceProvider.GetService<IContentDefinitionManager>();

        if (contentDefinitionManager is null)
        {
            return;
        }

        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (definition is null)
        {
            return;
        }

        // RolePickerPart is reusable by default. Look in the content type definition for the part.
        var roleNames = ContentAccessRestrictions.GetRoleNames(contentItem, definition);

        foreach (var roleName in roleNames)
        {
            if (user.IsInRole(roleName))
            {
                context.Succeed(requirement);

                return;
            }
        }

        if (roleNames.Count > 0)
        {
            context.Fail(new AuthorizationFailureReason(this, "User is not in any of the required roles for this content item."));
        }
    }
}
