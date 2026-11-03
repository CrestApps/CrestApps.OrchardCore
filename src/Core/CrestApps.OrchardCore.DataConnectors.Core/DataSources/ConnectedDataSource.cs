using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.DataSources;

/// <summary>
/// Exposes the live connected data sets: every read goes to the data set's external system, through its connection.
/// </summary>
internal sealed class ConnectedDataSource : IDataSource
{
    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectionStore _connectionStore;
    private readonly ConnectedDataSetAuthorizer _authorizer;
    private readonly ConnectedDataReader _reader;

    public ConnectedDataSource(
        IConnectedDataSetStore dataSetStore,
        IDataConnectionStore connectionStore,
        ConnectedDataSetAuthorizer authorizer,
        ConnectedDataReader reader)
    {
        _dataSetStore = dataSetStore;
        _connectionStore = connectionStore;
        _authorizer = authorizer;
        _reader = reader;
    }

    public static string Name => DataConnectorConstants.LiveDataSourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ConnectedDataSource>("Connected data (live)");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<ConnectedDataSource>("Data sets read live from external systems, such as databases, search indices and APIs, through data connections.");

    public async Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
    {
        var dataSets = await _dataSetStore.ListAsync(cancellationToken);
        var connections = (await _connectionStore.ListAsync(cancellationToken)).ToDictionary(connection => connection.ItemId, StringComparer.Ordinal);
        var descriptors = new List<DataSetDescriptor>();

        foreach (var dataSet in dataSets)
        {
            if (IsReadable(dataSet) && await _authorizer.CanReadAsync(context?.User, dataSet))
            {
                var group = connections.TryGetValue(dataSet.ConnectionId ?? string.Empty, out var connection) ? connection.DisplayText : null;

                descriptors.Add(ConnectedDataSetFields.ToDescriptor(dataSet, group));
            }
        }

        return descriptors;
    }

    public async Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
    {
        var record = await FindReadableAsync(dataSet, context, cancellationToken);

        if (record is null)
        {
            return null;
        }

        var connection = await _connectionStore.FindByIdAsync(record.ConnectionId, cancellationToken);

        return new DataSetSchema
        {
            DataSet = ConnectedDataSetFields.ToDescriptor(record, connection?.DisplayText),
            Fields = [.. ConnectedDataSetFields.ToDataFields(record)],
        };
    }

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var record = await FindReadableAsync(query.DataSet, query.Context, cancellationToken);

        if (record is null)
        {
            yield break;
        }

        await foreach (var batch in _reader.ReadAsync(record, query, DataConnectorReadPurpose.Live, cancellationToken))
        {
            yield return batch;
        }
    }

    private async Task<ConnectedDataSet> FindReadableAsync(string name, DataSourceContext context, CancellationToken cancellationToken)
    {
        var dataSet = await _dataSetStore.FindByNameAsync(name, cancellationToken);

        return IsReadable(dataSet) && await _authorizer.CanReadAsync(context?.User, dataSet)
            ? dataSet
            : null;
    }

    private static bool IsReadable(ConnectedDataSet dataSet)
        => dataSet is { IsEnabled: true, AccessMode: DataSetAccessMode.Live } && dataSet.Fields?.Count > 0;
}
