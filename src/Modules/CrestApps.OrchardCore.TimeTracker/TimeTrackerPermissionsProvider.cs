using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.TimeTracker;

internal sealed class TimeTrackerPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        TimeTrackerPermissions.ManageTimeTrackerSettings,
        TimeTrackerPermissions.ManageTimeEntries,
        TimeTrackerPermissions.ViewTimeEntries,
        TimeTrackerPermissions.TrackOwnTime,
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
        ];
}
