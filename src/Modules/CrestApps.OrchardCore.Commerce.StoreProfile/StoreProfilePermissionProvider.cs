using CrestApps.OrchardCore.Commerce;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Commerce.StoreProfile;

/// <summary>
/// Grants the store profile to administrators and store managers.
/// </summary>
internal sealed class StoreProfilePermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        StoreProfilePermissions.ManageStoreProfile,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                Name = CommerceConstants.StaffRoles.StoreManager,
                Permissions = _allPermissions,
            },
        ];
}
