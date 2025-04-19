using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.AI.Agents;

/// <summary>
/// These permissions mirror those in Orchard Core but are kept internal to avoid a direct dependency on Orchard Core modules.
/// This class should remain internal and only exists to decouple from module-level dependencies.
/// Once Orchard Core moves its permissions to a core library, this class can be removed in favor of referencing the core package directly.
/// </summary>
internal static class OrchardCorePermissions
{
    public static readonly Permission ManageFeatures = new("ManageFeatures", "Manage Features");

    public static readonly Permission ManageTenants = new("ManageTenants", "Manage tenants");

    public static readonly Permission ViewContentTypes = new("ViewContentTypes", "View content types.");

    public static readonly Permission EditContentTypes = new("EditContentTypes", "Edit content types.", isSecurityCritical: true);

    public static readonly Permission ManageRecipes = new Permission("ManageRecipes", "Manage Recipes", isSecurityCritical: true);
}
