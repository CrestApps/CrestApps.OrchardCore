using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DncRegistry.Services;

/// <summary>
/// Permission provider for the DNC Registry module.
/// </summary>
internal sealed class DncRegistryPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        DncRegistryPermissions.ManageDncRegistrySettings,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
}
