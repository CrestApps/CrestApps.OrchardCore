using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Finds the connection and connector of a data set once per request, for the drivers that each need them.
/// </summary>
internal sealed class DataSetConnectionResolver
{
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly Dictionary<string, DataConnection> _connections = new(StringComparer.Ordinal);

    public DataSetConnectionResolver(
        IDataConnectionStore connectionStore,
        IDataConnectorManager connectorManager)
    {
        _connectionStore = connectionStore;
        _connectorManager = connectorManager;
    }

    public async Task<DataConnection> GetConnectionAsync(ConnectedDataSet dataSet)
    {
        if (string.IsNullOrEmpty(dataSet?.ConnectionId))
        {
            return null;
        }

        if (!_connections.TryGetValue(dataSet.ConnectionId, out var connection))
        {
            connection = await _connectionStore.FindByIdAsync(dataSet.ConnectionId);
            _connections[dataSet.ConnectionId] = connection;
        }

        return connection;
    }

    public async Task<DataConnectorDescriptor> GetConnectorAsync(ConnectedDataSet dataSet)
    {
        var connection = await GetConnectionAsync(dataSet);

        return connection is null ? null : _connectorManager.GetDescriptor(connection.Connector);
    }
}
