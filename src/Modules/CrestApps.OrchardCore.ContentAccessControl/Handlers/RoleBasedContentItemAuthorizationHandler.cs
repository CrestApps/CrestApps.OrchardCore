using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Contents;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContentAccessControl.Handlers;

public sealed class RoleBasedContentItemAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    public RoleBasedContentItemAuthorizationHandler(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        if (context.HasSucceeded)
        {
            return;
        }

        var user = context?.User;

        if (user == null)
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

        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // RolePickerPart is reusable by default. Look in the content type definition for the part. 
        foreach (var partDefinition in definition.Parts)
        {
            var settings = partDefinition.GetSettings<RolePickerPartContentAccessControlSettings>();

            if (!settings.IsContentRestricted)
            {
                continue;
            }

            if (partDefinition.IsNamedPart())
            {
                var rolePickerPart = contentItem.Get<RolePickerPart>(partDefinition.Name);

                if (rolePickerPart?.RoleNames is null)
                {
                    continue;
                }

                foreach (var roleName in rolePickerPart.RoleNames)
                {
                    roleNames.Add(roleName);
                }
            }
            else
            {
                if (!contentItem.TryGet<RolePickerPart>(out var rolePickerPart) || rolePickerPart.RoleNames is null)
                {
                    continue;
                }

                foreach (var roleName in rolePickerPart.RoleNames)
                {
                    roleNames.Add(roleName);
                }
            }
        }

        foreach (var roleName in roleNames)
        {
            if (user.IsInRole(roleName))
            {
                context.Succeed(requirement);

                return;
            }
        }
    }
}
