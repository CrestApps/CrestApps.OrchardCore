using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.BusinessHours;

/// <summary>
/// Provides the Business Hours permissions.
/// </summary>
internal sealed class BusinessHoursPermissionsProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        BusinessHoursPermissions.ManageBusinessHoursSchedules,
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
