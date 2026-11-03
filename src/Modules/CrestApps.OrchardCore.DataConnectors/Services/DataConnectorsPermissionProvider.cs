using OrchardCore;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Lists the permissions of data connectors, with one read permission per connected data set, so a role can be let
/// read some data sets and not others.
/// </summary>
internal sealed class DataConnectorsPermissionProvider : IPermissionProvider
{
    private static readonly Permission[] _staticPermissions =
    [
        DataConnectorPermissions.ManageDataConnections,
        DataConnectorPermissions.ManageConnectedDataSets,
        DataConnectorPermissions.RefreshConnectedDataSets,
        DataConnectorPermissions.ReadAllConnectedDataSets,
    ];

    private readonly IConnectedDataSetStore _dataSetStore;

    public DataConnectorsPermissionProvider(IConnectedDataSetStore dataSetStore)
    {
        _dataSetStore = dataSetStore;
    }

    public async Task<IEnumerable<Permission>> GetPermissionsAsync()
    {
        var permissions = new List<Permission>(_staticPermissions);

        foreach (var dataSet in await _dataSetStore.ListAsync())
        {
            if (!string.IsNullOrEmpty(dataSet.Name))
            {
                permissions.Add(DataConnectorPermissions.CreateReadPermission(dataSet.Name));
            }
        }

        return permissions;
    }

    public IEnumerable<PermissionStereotype> GetDefaultStereotypes() =>
    [
        new PermissionStereotype
        {
            Name = OrchardCoreConstants.Roles.Administrator,
            Permissions = _staticPermissions,
        },
    ];
}
