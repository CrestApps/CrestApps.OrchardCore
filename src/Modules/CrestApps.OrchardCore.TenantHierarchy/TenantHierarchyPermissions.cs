using OrchardCore.Localization;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy;

/// <summary>
/// Contains the permissions of the tenant hierarchy.
/// </summary>
public static class TenantHierarchyPermissions
{
    /// <summary>
    /// Lets a Default tenant user make parent tenants, set their policies and act on the whole hierarchy.
    /// </summary>
    public static readonly Permission ManageTenantHierarchy = new(
        "ManageTenantHierarchy",
        LocalizationSource.Create("Manage the tenant hierarchy", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user create child tenants.
    /// </summary>
    public static readonly Permission CreateChildTenants = new(
        "CreateChildTenants",
        LocalizationSource.Create("Create child tenants", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user edit, suspend, resume and reload child tenants.
    /// </summary>
    public static readonly Permission ManageChildTenants = new(
        "ManageChildTenants",
        LocalizationSource.Create("Edit, suspend, resume and reload child tenants", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user enable and disable features in child tenants.
    /// </summary>
    public static readonly Permission ManageChildFeatures = new(
        "ManageChildFeatures",
        LocalizationSource.Create("Enable and disable features in child tenants", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user remove child tenants.
    /// </summary>
    public static readonly Permission RemoveChildTenants = new(
        "RemoveChildTenants",
        LocalizationSource.Create("Remove child tenants", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user decide who may enter child tenants and with which roles.
    /// </summary>
    public static readonly Permission ManageChildAccess = new(
        "ManageChildAccess",
        LocalizationSource.Create("Manage who may enter child tenants", typeof(TenantHierarchyPermissions)),
        isSecurityCritical: true);

    /// <summary>
    /// Lets a parent user see the child tenants admin and the hierarchy activity log.
    /// </summary>
    public static readonly Permission ViewChildTenants = new(
        "ViewChildTenants",
        LocalizationSource.Create("View child tenants", typeof(TenantHierarchyPermissions)),
        [CreateChildTenants, ManageChildTenants, ManageChildFeatures, RemoveChildTenants, ManageChildAccess]);

    /// <summary>
    /// The base gate for entering a child tenant. An access grant is still needed for each child tenant.
    /// </summary>
    public static readonly Permission EnterChildTenants = new(
        "EnterChildTenants",
        LocalizationSource.Create("Enter child tenants that an access grant covers", typeof(TenantHierarchyPermissions)));
}
