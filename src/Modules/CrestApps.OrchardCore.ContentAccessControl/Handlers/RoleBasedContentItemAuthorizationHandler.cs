using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Roles.Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
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

        if (user == null || ContentAccessRoles.SeesEverything(user))
        {
            return;
        }

        // Every permission that reads the item meets the restriction, its securable type's own included
        // (ViewContent_Handbook, ViewOwnContent_Handbook...), or a restricted item would leak through it.
        if (context.Resource == null || !IsReading(requirement.Permission?.Name))
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

        var roleNames = ContentAccessRestriction.GetRoleNames(definition, contentItem);

        if (roleNames.Count == 0)
        {
            return;
        }

        // Authenticated stands for anyone signed in and Anonymous for a visitor; neither is ever a role claim.
        if (ContentAccessRoles.IsInAnyRole(user, roleNames))
        {
            // A picked role lets its people view the item; listing and previewing still need their own permission.
            if (IsViewing(requirement.Permission.Name))
            {
                context.Succeed(requirement);
            }
        }
        else
        {
            context.Fail(new AuthorizationFailureReason(this, "User is not in any of the required roles for this content item."));
        }
    }

    /// <summary>
    /// Whether a permission reads content: viewing, previewing or listing it, whether one's own or anyone's, in general
    /// or for a securable type (<c>ViewContent_Handbook</c>). Compared by name, so a type's own permission meets the
    /// restriction too.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>Whether it reads content.</returns>
    internal static bool IsReading(string name)
        => !string.IsNullOrEmpty(name) && _reading.Contains(RootName(name));

    /// <summary>
    /// Whether a permission views content, in general or for a securable type: the ones a picked role grants.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>Whether it views content.</returns>
    internal static bool IsViewing(string name)
        => !string.IsNullOrEmpty(name) && RootName(name) == CommonPermissions.ViewContent.Name;

    // A securable type's permission is the general one's name, an underscore and the type: ViewContent_Handbook.
    private static string RootName(string name)
        => name.Split('_', 2)[0];

    private static readonly HashSet<string> _reading = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
        CommonPermissions.ListContent.Name,
    };
}
