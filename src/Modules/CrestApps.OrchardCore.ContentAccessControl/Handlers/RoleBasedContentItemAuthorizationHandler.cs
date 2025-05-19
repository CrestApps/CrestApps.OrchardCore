using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.ContentManagement;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContentAccessControl.Handlers;

public sealed class RoleBasedContentItemAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded)
        {
            return Task.CompletedTask;
        }

        var user = context?.User;

        if (user == null)
        {
            return Task.CompletedTask;
        }

        if (context.Resource == null ||
            (requirement.Permission != CommonPermissions.ViewContent && requirement.Permission != CommonPermissions.ViewOwnContent))
        {
            return Task.CompletedTask;
        }

        var contentItem = context.Resource as ContentItem;

        if (!contentItem.TryGet<RolePickerPart>(out var part) || part.RoleNames.Length == 0)
        {
            return Task.CompletedTask;
        }

        foreach (var roleName in part.RoleNames)
        {
            if (user.IsInRole(roleName))
            {
                context.Succeed(requirement);

                return Task.CompletedTask;
            }
        }

        return Task.CompletedTask;
    }
}
