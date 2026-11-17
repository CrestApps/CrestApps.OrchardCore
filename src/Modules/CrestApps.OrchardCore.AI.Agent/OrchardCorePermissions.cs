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

    /// <summary>
    /// Gets the permission to create, change, move and delete secrets. The Secrets module declares it in its module
    /// assembly.
    /// </summary>
    public static readonly Permission ManageSecrets = new("ManageSecrets", LocalizationSource.Create("Manage secrets", typeof(OrchardCorePermissions)));

    /// <summary>
    /// Gets the permission to list secrets, without their values. As in the Secrets module, the permission to manage
    /// secrets implies it.
    /// </summary>
    public static readonly Permission ViewSecrets = new("ViewSecrets", LocalizationSource.Create("View secrets", typeof(OrchardCorePermissions)), [ManageSecrets]);

    /// <summary>
    /// Gets the security-critical permission to see the details of the audit trail events about any user. The Users
    /// module declares it in its module assembly.
    /// </summary>
    public static readonly Permission ViewUserAuditTrailEvents = new(
        "ViewUserAuditTrailEvents",
        LocalizationSource.Create("View Audit Trail events about users", typeof(OrchardCorePermissions)),
        [],
        isSecurityCritical: true);

    /// <summary>
    /// Gets the security-critical permission to see the details of the audit trail events about the caller's own
    /// account. As in the Users module, the permission for the events about any user implies it.
    /// </summary>
    public static readonly Permission ViewOwnUserAuditTrailEvents = new(
        "ViewOwnUserAuditTrailEvents",
        LocalizationSource.Create("View Audit Trail events about own user", typeof(OrchardCorePermissions)),
        [ViewUserAuditTrailEvents],
        isSecurityCritical: true);

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
