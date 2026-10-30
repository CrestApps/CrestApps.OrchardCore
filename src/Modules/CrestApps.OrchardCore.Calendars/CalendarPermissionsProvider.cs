using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Calendars;

internal sealed class CalendarPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        CalendarPermissions.ManageAllCalendars,
        CalendarPermissions.ViewAllCalendars,
        CalendarPermissions.CreateCalendars,
        CalendarPermissions.AccessCalendars,
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
                Permissions = [CalendarPermissions.CreateCalendars],
            },
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [CalendarPermissions.AccessCalendars],
            },
        ];
    }
}
