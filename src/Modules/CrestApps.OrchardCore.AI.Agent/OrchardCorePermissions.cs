using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

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
}
