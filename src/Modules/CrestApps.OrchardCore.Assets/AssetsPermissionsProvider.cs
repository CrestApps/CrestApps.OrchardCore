using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Assets;

/// <summary>
/// Provides the Assets permissions.
/// </summary>
internal sealed class AssetsPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        AssetsPermissions.ManageAssets,
        AssetsPermissions.ViewAssets,
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
