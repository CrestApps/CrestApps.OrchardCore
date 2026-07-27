using CrestApps.OrchardCore.ContactCenter.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.ContactCenter.Services;

/// <summary>
/// Provides the permission exposed by the Contact Center preview maintenance feature.
/// </summary>
internal sealed class ContactCenterMaintenancePermissionProvider : IPermissionProvider
{
    private static readonly IEnumerable<Permission> _allPermissions =
    [
        ContactCenterPermissions.ManagePreviewData,
    ];

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

    /// <inheritdoc/>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
