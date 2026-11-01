using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Geo;

/// <summary>
/// Provides the Geo permissions.
/// </summary>
internal sealed class GeoPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        GeoPermissions.ManageTerritories,
        GeoPermissions.ManageGeoSettings,
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
