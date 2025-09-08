using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Services;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _allPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
        OmnichannelConstants.Permissions.ProcessActivity,
        OmnichannelConstants.Permissions.ManageDispositions,
    ];

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes()
        =>
        [
            new PermissionStereotype
            {
                Name = OrchardCoreConstants.Roles.Administrator,
                Permissions = _allPermissions,
            },
        ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
         => Task.FromResult(_allPermissions);
}
