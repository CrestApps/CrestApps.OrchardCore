using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Scheduling;

internal sealed class SchedulingPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        SchedulingPermissions.ManageScheduling,
        SchedulingPermissions.ManageAppointments,
        SchedulingPermissions.ViewAppointments,
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
                Name = OrchardCoreConstants.Roles.Editor,
                Permissions = [SchedulingPermissions.ManageAppointments],
            },
        ];
    }
}
