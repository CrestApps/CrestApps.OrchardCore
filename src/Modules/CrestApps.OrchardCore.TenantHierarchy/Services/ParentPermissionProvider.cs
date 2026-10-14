using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Declares the permissions of a parent tenant. Administrators get all of them.
/// </summary>
internal sealed class ParentPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions =
    [
        TenantHierarchyPermissions.ViewChildTenants,
        TenantHierarchyPermissions.CreateChildTenants,
        TenantHierarchyPermissions.ManageChildTenants,
        TenantHierarchyPermissions.ManageChildFeatures,
        TenantHierarchyPermissions.RemoveChildTenants,
        TenantHierarchyPermissions.ManageChildAccess,
        TenantHierarchyPermissions.EnterChildTenants,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_permissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _permissions,
        },
    ];
}
