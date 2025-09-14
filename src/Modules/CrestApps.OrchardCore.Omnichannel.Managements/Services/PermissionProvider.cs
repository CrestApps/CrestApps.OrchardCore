using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _agentPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
    ];

    private readonly IEnumerable<Permission> _allPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
        OmnichannelConstants.Permissions.ProcessActivity,
        OmnichannelConstants.Permissions.ManageDispositions,
        OmnichannelConstants.Permissions.ManageCampaigns,
        OmnichannelConstants.Permissions.ManageChannelEndpoints,
        OmnichannelConstants.Permissions.ManageActivityBatches,
    ];

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
                Name = OmnichannelConstants.AgentRole,
                Permissions = _agentPermissions,
            },
        ];

    public Task<IEnumerable<Permission>> GetPermissionsAsync()
         => Task.FromResult(_allPermissions);
}
