using CrestApps.OrchardCore.ContentAccessControl.Services;
using CrestApps.OrchardCore.Roles.Core.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata.Models;
using OrchardCore.Security;

namespace CrestApps.OrchardCore.ContentAccessControl.AI;

/// <summary>
/// What the content access tools share: the role names of the site and the description of a restricting part.
/// </summary>
internal static class ContentAccessRoleTools
{
    /// <summary>
    /// Returns the role names of the site, as the Role Picker editor lists them.
    /// </summary>
    /// <param name="services">The services of the tool invocation.</param>
    /// <returns>The role names.</returns>
    public static IReadOnlyList<string> GetRoleNames(IServiceProvider services)
        => services.GetRequiredService<RoleManager<IRole>>().Roles
            .Select(role => role.RoleName)
            .ToList()
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();

    /// <summary>
    /// Describes a restricting part of a content item: its name, the roles of the published and latest versions, and the
    /// rules its settings set.
    /// </summary>
    /// <param name="partDefinition">The part definition.</param>
    /// <param name="published">The published version, or <see langword="null"/>.</param>
    /// <param name="latest">The latest version.</param>
    /// <param name="roleNames">The role names of the site.</param>
    /// <returns>The description.</returns>
    public static object DescribePart(ContentTypePartDefinition partDefinition, ContentItem published, ContentItem latest, IReadOnlyList<string> roleNames)
    {
        var settings = partDefinition.GetSettings<RolePickerPartSettings>();
        var publishedRoles = published is null ? null : ContentAccessRestrictions.GetPart(published, partDefinition)?.RoleNames ?? [];
        var latestRoles = ContentAccessRestrictions.GetPart(latest, partDefinition)?.RoleNames ?? [];
        var hasDraft = published is null || published.ContentItemVersionId != latest.ContentItemVersionId;

        return new
        {
            partName = partDefinition.Name,
            displayName = partDefinition.DisplayName(),
            roles = publishedRoles,
            draftRoles = hasDraft ? latestRoles : null,
            required = settings.Required,
            allowSelectMultiple = settings.AllowSelectMultiple,
            excludedRoles = settings.ExcludedRoles ?? [],
            availableRoles = roleNames.Except(settings.ExcludedRoles ?? [], StringComparer.OrdinalIgnoreCase).ToList(),
        };
    }
}
