using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TenantHierarchy.Services;

/// <summary>
/// Declares the permission of the platform. Administrators of the Default tenant get it.
/// </summary>
internal sealed class PlatformPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _permissions =
    [
        TenantHierarchyPermissions.ManageTenantHierarchy,
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
