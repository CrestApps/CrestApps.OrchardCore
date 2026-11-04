using CrestApps.OrchardCore.Workforce.Models;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Workforce;

/// <summary>
/// The time clock's permissions. Administrators have them all; every signed-in user may clock in and out, which needs
/// a worker linked to their account and a team with a time clock policy.
/// </summary>
internal sealed class TimeClockPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        WorkforcePermissions.ManageTimeClockPolicies,
        WorkforcePermissions.ApproveTimesheets,
        WorkforcePermissions.EditPunches,
        WorkforcePermissions.ExportPayroll,
        WorkforcePermissions.ViewPayRates,
        WorkforcePermissions.PunchTime,
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
                Name = OrchardCoreConstants.Roles.Authenticated,
                Permissions = [WorkforcePermissions.PunchTime],
            },
        ];
    }
}
