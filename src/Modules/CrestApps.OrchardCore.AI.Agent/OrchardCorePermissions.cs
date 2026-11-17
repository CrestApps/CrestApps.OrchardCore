using OrchardCore.Contents;
using OrchardCore.Contents.Security;
using OrchardCore.Localization;
using OrchardCore.Security.Permissions;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.AI.Agent;

/// <summary>
/// These permissions mirror those in Orchard Core but are kept internal to avoid a direct dependency on Orchard Core modules.
/// This class should remain internal and only exists to decouple from module-level dependencies.
/// Once Orchard Core moves its permissions to a core library, this class can be removed in favor of referencing the core package directly.
/// </summary>
internal static class OrchardCorePermissions
{
    /// <summary>
    /// Gets the permission to manage features.
    /// </summary>
    public static readonly Permission ManageFeatures = new("ManageFeatures", LocalizationSource.Create("Manage Features", typeof(OrchardCorePermissions)));

    /// <summary>
    /// Gets the permission to manage tenants.
    /// </summary>
    public static readonly Permission ManageTenants = new("ManageTenants", LocalizationSource.Create("Manage tenants", typeof(OrchardCorePermissions)));

    /// <summary>
    /// Gets the permission to view content types.
    /// </summary>
    public static readonly Permission ViewContentTypes = new("ViewContentTypes", LocalizationSource.Create("View content types.", typeof(OrchardCorePermissions)));

    /// <summary>
    /// Gets the security-critical permission to edit content types.
    /// </summary>
    public static readonly Permission EditContentTypes = new("EditContentTypes", LocalizationSource.Create("Edit content types.", typeof(OrchardCorePermissions)), isSecurityCritical: true);

    /// <summary>
    /// Gets the security-critical permission to manage recipes.
    /// </summary>
    public static readonly Permission ManageRecipes = new("ManageRecipes", LocalizationSource.Create("Manage Recipes", typeof(OrchardCorePermissions)), isSecurityCritical: true);

    /// <summary>
    /// Gets the security-critical permission to manage workflows.
    /// </summary>
    public static readonly Permission ManageWorkflows = new("ManageWorkflows", LocalizationSource.Create("Manage workflows", typeof(OrchardCorePermissions)), isSecurityCritical: true);

    /// <summary>
    /// Gets the permission to manage menus. As in the Menu module, the permission to edit Menu content items implies it.
    /// </summary>
    public static readonly Permission ManageMenu = new(
        "ManageMenu",
        LocalizationSource.Create("Manage menus", typeof(OrchardCorePermissions)),
        [ContentTypePermissionsHelper.CreateDynamicPermissionOf(CommonPermissions.EditContent.Name, "Menu")]);

    /// <summary>
    /// Gets the permission to manage taxonomies. Its name is singular in the Taxonomies module.
    /// </summary>
    public static readonly Permission ManageTaxonomies = new("ManageTaxonomy", LocalizationSource.Create("Manage taxonomies", typeof(OrchardCorePermissions)));

    /// <summary>
    /// Gets the security-critical permission to run SQL queries. Orchard Core declares it in OrchardCore.Queries.Core,
    /// a package this repository does not reference.
    /// </summary>
    public static readonly Permission ManageSqlQueries = new("ManageSqlQueries", LocalizationSource.Create("Manage SQL Queries", typeof(OrchardCorePermissions)), isSecurityCritical: true);

    // The Users module declares this template in its module assembly. As there, the permission to manage users implies it.
    private static readonly PermissionTemplate _manageOwnCustomUserSettings = new(
        "ManageOwnCustomUserSettings_{0}",
        LocalizationSource.Create("Manage Own Custom User Settings - {0}", typeof(OrchardCorePermissions)),
        UsersPermissions.ManageUsers);

    /// <summary>
    /// Creates the permission to edit the values of one custom user settings type. Despite its name, the user editor
    /// checks it for whichever user is edited.
    /// </summary>
    /// <param name="settingsType">The technical name of the custom user settings content type.</param>
    /// <returns>The permission for that settings type.</returns>
    public static Permission CreateManageOwnCustomUserSettings(string settingsType)
        => _manageOwnCustomUserSettings.CreateDynamicPermission(settingsType);
}
