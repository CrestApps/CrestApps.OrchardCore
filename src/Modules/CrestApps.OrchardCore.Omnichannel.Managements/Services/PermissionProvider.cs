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
        OmnichannelConstants.Permissions.CompleteOwnActivity,
        OmnichannelConstants.Permissions.EditActivity,
    ];

    // A supervisor reads every report; the reports of each feature are granted by that feature's own provider.
    private readonly IEnumerable<Permission> _supervisorPermissions =
    [
        OmnichannelConstants.Permissions.ViewReports,
    ];

    private readonly IEnumerable<Permission> _allPermissions =
    [
        OmnichannelConstants.Permissions.ListActivities,
        OmnichannelConstants.Permissions.ListContactActivities,
        OmnichannelConstants.Permissions.CompleteActivity,
        OmnichannelConstants.Permissions.CompleteOwnActivity,
        OmnichannelConstants.Permissions.EditActivity,
        OmnichannelConstants.Permissions.ManageActivities,
        OmnichannelConstants.Permissions.PurgeActivity,
        OmnichannelConstants.Permissions.ManageDispositions,
        OmnichannelConstants.Permissions.ManageCampaigns,
        OmnichannelConstants.Permissions.ManageCadences,
        OmnichannelConstants.Permissions.ManageCampaignGroups,
        OmnichannelConstants.Permissions.ManageChannelEndpoints,
        OmnichannelConstants.Permissions.ManageActivityBatches,
        OmnichannelConstants.Permissions.DeleteLoadedActivityBatches,
        OmnichannelConstants.Permissions.ManageSubjectFlows,
        OmnichannelConstants.Permissions.ViewReports,
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
        new PermissionStereotype
        {
            Name = OmnichannelConstants.SupervisorRole,
            Permissions = _supervisorPermissions,
        },
    ];

    /// <summary>
    /// Retrieves the permissions async.
    /// </summary>
    public Task<IEnumerable<Permission>> GetPermissionsAsync()
        => Task.FromResult(_allPermissions);
}
