using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Staff;

internal sealed class StaffPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        StaffPermissions.ManageStaff,
        StaffPermissions.ManageOwnStaff,
        StaffPermissions.ViewStaff,
        StaffPermissions.ViewOwnStaffProfile,
        StaffPermissions.ViewSensitiveStaffData,
        StaffPermissions.ManageStaffPay,
    ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
    {
        return
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
            new PermissionStereotype
            {
                // Everyone Staff puts in its role sees their own page.
                Name = StaffConstants.DefaultStaffRoleName,
                Permissions = [StaffPermissions.ViewOwnStaffProfile],
            },
        ];
    }
}
