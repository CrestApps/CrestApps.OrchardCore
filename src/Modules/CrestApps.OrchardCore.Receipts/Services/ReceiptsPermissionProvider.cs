using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Receipts.Services;

/// <summary>
/// Provides the permissions exposed by the Receipts feature.
/// </summary>
internal sealed class ReceiptsPermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        ReceiptsPermissions.ManageReceiptSettings,
    ];

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    /// <inheritdoc/>
    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];
    }
}
