using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

internal sealed class WorkforcePermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.ManageAllWorkforce,
        WorkforcePermissions.ManageTeams,
        WorkforcePermissions.ManageWorkers,
        WorkforcePermissions.ViewWorkers,
        WorkforcePermissions.ViewSensitiveWorkerData,
        WorkforcePermissions.ManageActivityCodes,
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
