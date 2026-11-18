using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentAccessControl.Services;

/// <summary>
/// Reads which Role Picker parts of a content type restrict who may view its items, and the roles an item names in them.
/// The authorization handler and the AI tools read them the same way.
/// </summary>
internal static class ContentAccessRestrictions
{
    /// <summary>
    /// Returns the parts of a content type whose "Restrict content" setting is on. A Role Picker part is reusable, so a
    /// type can have several, each under its own name.
    /// </summary>
    /// <param name="definition">The content type definition.</param>
    /// <returns>The restricting parts.</returns>
    public static IReadOnlyList<ContentTypePartDefinition> GetRestrictingParts(ContentTypeDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return definition.Parts
            .Where(partDefinition => partDefinition.GetSettings<RolePickerPartContentAccessControlSettings>().IsContentRestricted)
            .ToList();
    }

    /// <summary>
    /// Returns the Role Picker part of a content item for a part definition, or <see langword="null"/> when the item has
    /// none.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="partDefinition">The part definition.</param>
    /// <returns>The part, or <see langword="null"/>.</returns>
    public static RolePickerPart GetPart(ContentItem contentItem, ContentTypePartDefinition partDefinition)
    {
        ArgumentNullException.ThrowIfNull(contentItem);
        ArgumentNullException.ThrowIfNull(partDefinition);

        if (partDefinition.IsNamedPart())
        {
            return contentItem.Get<RolePickerPart>(partDefinition.Name);
        }

        return contentItem.TryGet<RolePickerPart>(out var part)
            ? part
            : null;
    }

    /// <summary>
    /// Returns the roles that may view a content item: the roles named in every restricting part. An empty result means
    /// the item is not restricted.
    /// </summary>
    /// <param name="contentItem">The content item.</param>
    /// <param name="definition">Its content type definition.</param>
    /// <returns>The role names.</returns>
    public static HashSet<string> GetRoleNames(ContentItem contentItem, ContentTypeDefinition definition)
    {
        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var partDefinition in GetRestrictingParts(definition))
        {
            var rolePickerPart = GetPart(contentItem, partDefinition);

            if (rolePickerPart?.RoleNames is null)
            {
                continue;
            }

            foreach (var roleName in rolePickerPart.RoleNames)
            {
                roleNames.Add(roleName);
            }
        }

        return roleNames;
    }
}
