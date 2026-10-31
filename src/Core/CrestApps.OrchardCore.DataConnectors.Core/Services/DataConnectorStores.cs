using CrestApps.OrchardCore.DataConnectors.Indexes;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.YesSql.Core.Services;
using YesSql;

namespace CrestApps.OrchardCore.DataConnectors.Services;

internal sealed class DataConnectionStore : DocumentCatalog<DataConnection, DataConnectionIndex>, IDataConnectionStore
{
    public DataConnectionStore(ISession session)
        : base(session)
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    public async Task<DataConnection> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var all = await ListAsync(cancellationToken);

        return all.FirstOrDefault(connection => string.Equals(connection.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken cancellationToken = default)
    {
        var connections = await Session.Query<DataConnection, DataConnectionIndex>(collection: CollectionName).ListAsync(cancellationToken);

        return [.. connections.OrderBy(connection => connection.DisplayText ?? connection.Name, StringComparer.OrdinalIgnoreCase)];
    }
}

internal sealed class ConnectedDataSetStore : DocumentCatalog<ConnectedDataSet, ConnectedDataSetIndex>, IConnectedDataSetStore
{
    public ConnectedDataSetStore(ISession session)
        : base(session)
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    public async Task<ConnectedDataSet> FindByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var all = await ListAsync(cancellationToken);

        return all.FirstOrDefault(dataSet => string.Equals(dataSet.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    public async Task<IReadOnlyList<ConnectedDataSet>> ListAsync(CancellationToken cancellationToken = default)
    {
        var dataSets = await Session.Query<ConnectedDataSet, ConnectedDataSetIndex>(collection: CollectionName).ListAsync(cancellationToken);

        return [.. dataSets.OrderBy(dataSet => dataSet.DisplayText ?? dataSet.Name, StringComparer.OrdinalIgnoreCase)];
    }

    public async Task<IReadOnlyList<ConnectedDataSet>> ListByConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(connectionId))
        {
            return [];
        }

        return [.. await Session.Query<ConnectedDataSet, ConnectedDataSetIndex>(index => index.ConnectionId == connectionId, collection: CollectionName).ListAsync(cancellationToken)];
    }

    public async Task<IReadOnlyList<ConnectedDataSet>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
    {
        var imported = DataSetAccessMode.Imported;
        var dataSets = await Session.Query<ConnectedDataSet, ConnectedDataSetIndex>(
                index => index.IsEnabled && index.AccessMode == imported && (index.RefreshRequestedUtc != null || index.NextRefreshUtc <= utcNow),
                collection: CollectionName)
            .ListAsync(cancellationToken);

        // A refresh someone asked for goes before a scheduled one, then the most overdue first.
        return
        [
            .. dataSets
                .OrderBy(dataSet => dataSet.Import?.RefreshRequestedUtc is null)
                .ThenBy(dataSet => dataSet.Import?.RefreshRequestedUtc ?? dataSet.Import?.NextRefreshUtc ?? utcNow),
        ];
    }
}

internal sealed class DataSetRefreshRunStore : DocumentCatalog<DataSetRefreshRun, DataSetRefreshRunIndex>, IDataSetRefreshRunStore
{
    public DataSetRefreshRunStore(ISession session)
        : base(session)
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    public async Task<(int Count, IReadOnlyList<DataSetRefreshRun> Runs)> PageAsync(string dataSetId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var query = Session.Query<DataSetRefreshRun, DataSetRefreshRunIndex>(index => index.DataSetId == dataSetId, collection: CollectionName);
        var count = await query.CountAsync(cancellationToken);
        var page = await query.OrderByDescending(index => index.StartedUtc).Skip(skip).Take(take).ListAsync(cancellationToken);

        return (count, [.. page]);
    }

    public async Task<int> PruneAsync(string dataSetId, int keep, CancellationToken cancellationToken = default)
    {
        var old = await Session.Query<DataSetRefreshRun, DataSetRefreshRunIndex>(index => index.DataSetId == dataSetId, collection: CollectionName)
            .OrderByDescending(index => index.StartedUtc)
            .Skip(Math.Max(0, keep))
            .Take(500)
            .ListAsync(cancellationToken);
        var count = 0;

        foreach (var run in old)
        {
            Session.Delete(run, CollectionName);
            count++;
        }

        return count;
    }
}
