using CrestApps.OrchardCore.Omnichannel.Core;
using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Services;

internal sealed class PermissionProvider : IPermissionProvider
{
    private readonly IEnumerable<Permission> _agentPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
        OmnichannelConstants.Permissions.ListContactActivities,
    ];

    private readonly IEnumerable<Permission> _allPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
        OmnichannelConstants.Permissions.ListContactActivities,
        OmnichannelConstants.Permissions.CompleteActivity,
        OmnichannelConstants.Permissions.ManageDispositions,
        OmnichannelConstants.Permissions.ManageCampaigns,
        OmnichannelConstants.Permissions.ManageChannelEndpoints,
        OmnichannelConstants.Permissions.ManageActivityBatches,
    ];

    /// <summary>
    /// Retrieves the default stereotypes.
    /// </summary>
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

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
