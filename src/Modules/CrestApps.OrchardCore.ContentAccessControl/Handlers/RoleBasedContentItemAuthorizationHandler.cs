using CrestApps.OrchardCore.ContentAccessControl.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContentAccessControl.Handlers;

/// <summary>
/// Refuses a restricted content item to whoever isn't in one of its roles: viewing it, previewing it and cloning it (a
/// preview or a clone shows everything the item holds).
/// </summary>
public sealed class RoleBasedContentItemAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    // Compared by name: a permission object isn't the same instance in every module.
    private static readonly HashSet<string> _guardedPermissions = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
        CommonPermissions.CloneContent.Name,
        CommonPermissions.CloneOwnContent.Name,
    };

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
        // It runs even when the permission was already granted, as View content is to everyone, anonymous visitors
        // included: a restricted item is refused to whoever isn't in its roles, whatever else allowed it.
        var user = context?.User;

        if (user == null || user.IsInRole(OrchardCoreConstants.Roles.Administrator))
        {
            return;
        }

        if (context.Resource is not ContentItem contentItem || requirement.Permission is null || !_guardedPermissions.Contains(requirement.Permission.Name))
        {
            return;
        }

        var roleNames = await ContentAccessRestrictions.GetRoleNamesAsync(contentItem, _serviceProvider.GetService<IContentDefinitionManager>());

        if (roleNames.Count == 0)
        {
            return;
        }

        if (roleNames.Any(user.IsInRole))
        {
            context.Succeed(requirement);

            return;
        }

        context.Fail(new AuthorizationFailureReason(this, "User is not in any of the required roles for this content item."));
    }
}
