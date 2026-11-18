using CrestApps.OrchardCore.Roles.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;

namespace CrestApps.OrchardCore.ContentAccessControl.Services;

/// <summary>
/// Reads the roles a content item is restricted to, for the authorization handler and the index alike.
/// </summary>
internal static class ContentAccessRestriction
{
    /// <summary>
    /// Whether a content type restricts its items: a <see cref="RolePickerPart"/> on it has <c>Restrict content?</c> on.
    /// </summary>
    /// <param name="definition">The content type.</param>
    /// <returns>Whether it restricts its items.</returns>
    public static bool IsRestricted(ContentTypeDefinition definition)
        => definition?.Parts.Any(IsRestricting) == true;

    /// <summary>
    /// The roles picked on an item, from every <see cref="RolePickerPart"/> of its type (named or not) that restricts
    /// content. Empty when none is picked: the item is open to everyone.
    /// </summary>
    /// <param name="definition">The item's content type.</param>
    /// <param name="contentItem">The item.</param>
    /// <returns>The role names.</returns>
    public static HashSet<string> GetRoleNames(ContentTypeDefinition definition, ContentItem contentItem)
    {
        var roleNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (definition is null)
        {
            return roleNames;
        }

        // RolePickerPart is reusable by default. Look in the content type definition for the part.
        foreach (var partDefinition in definition.Parts.Where(IsRestricting))
        {
            var part = partDefinition.IsNamedPart()
                ? contentItem.Get<RolePickerPart>(partDefinition.Name)
                : contentItem.TryGet<RolePickerPart>(out var unnamed) ? unnamed : null;

            foreach (var roleName in part?.RoleNames ?? [])
            {
                if (!string.IsNullOrWhiteSpace(roleName))
                {
                    roleNames.Add(roleName);
                }
            }
        }

        return roleNames;
    }

    private static bool IsRestricting(ContentTypePartDefinition partDefinition)
        => partDefinition.PartDefinition?.Name == nameof(RolePickerPart)
            && partDefinition.GetSettings<RolePickerPartContentAccessControlSettings>().IsContentRestricted;
}
