using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// The actions of the connections and data sets pages that change stored records, shared by the admin controllers and
/// the AI tools: deleting a connection no data set reads, deleting a data set with its rows and refreshes, and asking
/// for a refresh.
/// </summary>
public sealed class DataConnectorCommands
{
    private readonly IDataConnectionStore _connectionStore;
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataSetRefreshRunStore _runStore;
    private readonly IDataSetRowStore _rowStore;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectorCommands"/> class.
    /// </summary>
    /// <param name="connectionStore">The connections.</param>
    /// <param name="dataSetStore">The data sets.</param>
    /// <param name="runStore">The refreshes.</param>
    /// <param name="rowStore">The rows of imported data sets.</param>
    /// <param name="clock">The clock.</param>
    public DataConnectorCommands(
        IDataConnectionStore connectionStore,
        IConnectedDataSetStore dataSetStore,
        IDataSetRefreshRunStore runStore,
        IDataSetRowStore rowStore,
        IClock clock)
    {
        _connectionStore = connectionStore;
        _dataSetStore = dataSetStore;
        _runStore = runStore;
        _rowStore = rowStore;
        _clock = clock;
    }

    /// <summary>
    /// Deletes a connection, with its stored credentials, unless a data set still reads it.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">The token that cancels the deletion.</param>
    /// <returns>The data sets that read the connection, which kept it from being deleted; empty when it was deleted.</returns>
    public async Task<IReadOnlyList<ConnectedDataSet>> DeleteConnectionAsync(DataConnection connection, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var dataSets = await _dataSetStore.ListByConnectionAsync(connection.ItemId, cancellationToken);

        if (dataSets.Count > 0)
        {
            return dataSets;
        }

        await _connectionStore.DeleteAsync(connection, cancellationToken);

        return [];
    }

    /// <summary>
    /// Deletes a data set with its imported rows and its refreshes.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the deletion.</param>
    /// <returns>A task.</returns>
    public async Task DeleteDataSetAsync(ConnectedDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        await _rowStore.DeleteAllAsync(dataSet.ItemId, cancellationToken: cancellationToken);
        await _runStore.PruneAsync(dataSet.ItemId, 0, cancellationToken);
        await _dataSetStore.DeleteAsync(dataSet, cancellationToken);
    }

    /// <summary>
    /// Records on a data set that a refresh was asked for. The refresh task runs it within a minute, unless the caller
    /// runs it sooner.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the update.</param>
    /// <returns>A task.</returns>
    public async Task RequestRefreshAsync(ConnectedDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        dataSet.Import ??= new DataSetImportState();
        dataSet.Import.RefreshRequestedUtc = _clock.UtcNow;
        await _dataSetStore.UpdateAsync(dataSet, cancellationToken);
    }
}
