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
/// Refuses a restricted content item to whoever isn't in one of its roles: viewing it, listing it, previewing it and
/// cloning it (a preview or a clone shows everything the item holds), and opening it for editing (the editor shows every
/// field), publishing or deleting it. Being in one of its roles grants viewing it only; changing it still needs the
/// permission to change it.
/// </summary>
public sealed class RoleBasedContentItemAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceProvider _serviceProvider;

    // The roles of each item's current latest and published versions, read once a request (the handler is scoped).
    private readonly Dictionary<string, IReadOnlyList<HashSet<string>>> _currentRoles = new(StringComparer.Ordinal);

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

        // Every permission that reads or changes the item meets the restriction, its securable type's own included
        // (ViewContent_Handbook, EditOwnContent_Handbook...), or a restricted item would leak through it.
        if (context.Resource == null || !IsRestricted(requirement.Permission?.Name))
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

        if (!ContentAccessRestriction.IsRestricted(definition))
        {
            return;
        }

        // The version passed in may be an old one, such as an audit trail snapshot opened or restored: the restriction of
        // the item as it is now applies too, or a user outside its current roles could open or restore an older version
        // and remove or widen it.
        var roleSets = new List<HashSet<string>> { ContentAccessRestriction.GetRoleNames(definition, contentItem) };

        roleSets.AddRange(await GetCurrentRoleSetsAsync(contentItem, definition));
        roleSets.RemoveAll(roles => roles.Count == 0);

        if (roleSets.Count == 0)
        {
            return;
        }

        // Authenticated stands for anyone signed in and Anonymous for a visitor; neither is ever a role claim.
        if (roleSets.All(roles => ContentAccessRoles.IsInAnyRole(user, roles)))
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
    /// Whether a permission reads or changes content: viewing, previewing, listing or cloning it (a clone copies
    /// everything the item holds), and editing (the editor shows every field), publishing or deleting it, whether one's
    /// own or anyone's, in general or for a securable type (<c>ViewContent_Handbook</c>). Compared by name, so a type's own
    /// permission meets the restriction too.
    /// </summary>
    /// <param name="name">The permission's name.</param>
    /// <returns>Whether a restricted item refuses it to whoever isn't in its roles.</returns>
    // The roles of the item's current latest and published versions other than the one passed in, or none for a new item.
    private async Task<IReadOnlyList<HashSet<string>>> GetCurrentRoleSetsAsync(ContentItem contentItem, global::OrchardCore.ContentManagement.Metadata.Models.ContentTypeDefinition definition)
    {
        if (string.IsNullOrEmpty(contentItem.ContentItemId) || _serviceProvider.GetService<IContentManager>() is not { } contentManager)
        {
            return [];
        }

        if (_currentRoles.TryGetValue(contentItem.ContentItemId, out var cached))
        {
            return cached;
        }

        // Kept empty while the versions load, so a content handler that checks a permission meanwhile can't loop back here.
        _currentRoles[contentItem.ContentItemId] = [];

        var sets = new List<HashSet<string>>();

        foreach (var options in new[] { VersionOptions.Latest, VersionOptions.Published })
        {
            var current = await contentManager.GetAsync(contentItem.ContentItemId, options);

            if (current is not null && !string.Equals(current.ContentItemVersionId, contentItem.ContentItemVersionId, StringComparison.Ordinal))
            {
                sets.Add(ContentAccessRestriction.GetRoleNames(definition, current));
            }
        }

        _currentRoles[contentItem.ContentItemId] = sets;

        return sets;
    }

    internal static bool IsRestricted(string name)
        => !string.IsNullOrEmpty(name) && _restricted.Contains(RootName(name));

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

    private static readonly HashSet<string> _restricted = new(StringComparer.Ordinal)
    {
        CommonPermissions.ViewContent.Name,
        CommonPermissions.ViewOwnContent.Name,
        CommonPermissions.PreviewContent.Name,
        CommonPermissions.PreviewOwnContent.Name,
        CommonPermissions.ListContent.Name,
        CommonPermissions.CloneContent.Name,
        CommonPermissions.CloneOwnContent.Name,
        CommonPermissions.EditContent.Name,
        CommonPermissions.EditOwnContent.Name,
        CommonPermissions.PublishContent.Name,
        CommonPermissions.PublishOwnContent.Name,
        CommonPermissions.DeleteContent.Name,
        CommonPermissions.DeleteOwnContent.Name,
    };
}
