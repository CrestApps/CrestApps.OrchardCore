using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

internal sealed class ShiftPlanningPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.ManageShiftTemplates,
        WorkforcePermissions.ManageSchedules,
        WorkforcePermissions.PublishSchedules,
        WorkforcePermissions.ManageOpenShifts,
        WorkforcePermissions.ApproveShiftTrades,
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
        ];
    }
}
