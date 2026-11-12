using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentAccessControl.Services;

/// <summary>
/// Reads which roles a content item is restricted to: the roles of every Role Picker part whose settings restrict the
/// content. An empty set means the item isn't restricted.
/// </summary>
internal static class ContentAccessRestrictions
{
    /// <summary>
    /// Returns the roles the item is restricted to, or an empty set when it isn't restricted.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    public static async Task<IReadOnlySet<string>> GetRoleNamesAsync(ContentItem contentItem, IContentDefinitionManager contentDefinitionManager)
    {
        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (contentItem is null || contentDefinitionManager is null)
        {
            return roleNames;
        }

        var definition = await contentDefinitionManager.GetTypeDefinitionAsync(contentItem.ContentType);

        if (definition is null)
        {
            return roleNames;
        }

        // RolePickerPart is reusable by default. Look in the content type definition for the part.
        foreach (var partDefinition in definition.Parts)
        {
            if (!partDefinition.GetSettings<RolePickerPartContentAccessControlSettings>().IsContentRestricted)
            {
                continue;
            }

            var part = partDefinition.IsNamedPart()
                ? contentItem.Get<RolePickerPart>(partDefinition.Name)
                : contentItem.TryGet<RolePickerPart>(out var unnamed) ? unnamed : null;

            foreach (var roleName in part?.RoleNames ?? [])
            {
                roleNames.Add(roleName);
            }
        }

        return roleNames;
    }
}
