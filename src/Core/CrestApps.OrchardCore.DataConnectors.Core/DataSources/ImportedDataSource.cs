using System.Globalization;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;
using CrestApps.OrchardCore.DataConnectors.Storage;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.DataSources;

/// <summary>
/// Exposes the imported connected data sets: reads are served from the rows of the last refresh that succeeded, in the
/// tenant's database, so the data set stays available when its external system isn't.
/// </summary>
internal sealed class ImportedDataSource : IDataSource
{
    private const int _keysPerRead = 500;

    private readonly IConnectedDataSetStore _dataSetStore;
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataSetRowStore _rowStore;
    private readonly ConnectedDataSetAuthorizer _authorizer;

    public ImportedDataSource(
        IConnectedDataSetStore dataSetStore,
        IDataConnectionStore connectionStore,
        IDataSetRowStore rowStore,
        ConnectedDataSetAuthorizer authorizer)
    {
        _dataSetStore = dataSetStore;
        _connectionStore = connectionStore;
        _rowStore = rowStore;
        _authorizer = authorizer;
    }

    public static string Name => DataConnectorConstants.ImportedDataSourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<ImportedDataSource>("Connected data (imported)");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<ImportedDataSource>("Data sets imported from external systems through data connections and refreshed on a schedule.");

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

                descriptors.Add(Describe(dataSet, group));
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
            DataSet = Describe(record, connection?.DisplayText),
            Fields = [.. ConnectedDataSetFields.ToDataFields(record, identifiersAreKeyFilterable: true)],
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

        var mapper = ConnectedDataSetMapper.Create(record, query.Fields, identifiersAreKeyFilterable: true);
        var fields = mapper.Fields;
        var version = record.Import?.ActiveVersion;
        var batchSize = query.BatchSize > 0 ? query.BatchSize : DataSourceQuery.DefaultBatchSize;
        var remaining = query.MaxRows > 0 ? query.MaxRows : long.MaxValue;
        var any = false;

        if (string.IsNullOrEmpty(version))
        {
            // Not imported yet: the data set is empty, but its fields are known.
            yield return new DataBatch(fields, []);

            yield break;
        }

        var keyChunks = GetKeys(record, query.Conditions)?.Chunk(_keysPerRead).ToArray();

        foreach (var keys in keyChunks ?? [null])
        {
            long afterId = 0;

            while (remaining > 0)
            {
                var take = (int)Math.Min(batchSize, remaining);
                var stored = await _rowStore.ReadAsync(record.ItemId, version, afterId, take, keys, cancellationToken);

                if (stored.Count == 0)
                {
                    break;
                }

                var rows = new List<object[]>(stored.Count);

                foreach (var row in stored)
                {
                    var values = new object[fields.Count];

                    for (var index = 0; index < fields.Count; index++)
                    {
                        values[index] = DataSetRows.Read(row.Data, fields[index]);
                    }

                    rows.Add(values);
                }

                afterId = stored[^1].Id;
                remaining -= rows.Count;
                any = true;

                yield return new DataBatch(fields, rows);

                if (stored.Count < take)
                {
                    break;
                }
            }
        }

        if (!any)
        {
            yield return new DataBatch(fields, []);
        }
    }

    // A single identifier filtered by Equals or In is read by key; any other condition is left to the consumer.
    private static string[] GetKeys(ConnectedDataSet dataSet, IEnumerable<DataCondition> conditions)
    {
        var identifiers = dataSet.Fields.Where(field => field.IsIncluded && field.IsIdentifier).ToArray();

        if (identifiers.Length != 1)
        {
            return null;
        }

        var identifier = identifiers[0];
        var values = DataConditions.Keys(conditions, identifier.Name);

        return values?
            .Select(value => DataSetRows.ToKey([ConnectedDataSetMapper.Coerce(value, identifier.Type)]))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static DataSetDescriptor Describe(ConnectedDataSet dataSet, string group)
    {
        var descriptor = ConnectedDataSetFields.ToDescriptor(dataSet, group);

        if (dataSet.Import?.LastSuccessUtc is { } refreshed)
        {
            var refreshedText = refreshed.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

            descriptor.Description = string.IsNullOrWhiteSpace(descriptor.Description)
                ? $"Imported {refreshedText}."
                : $"{descriptor.Description} (imported {refreshedText})";
        }

        return descriptor;
    }

    private async Task<ConnectedDataSet> FindReadableAsync(string name, DataSourceContext context, CancellationToken cancellationToken)
    {
        var dataSet = await _dataSetStore.FindByNameAsync(name, cancellationToken);

        return IsReadable(dataSet) && await _authorizer.CanReadAsync(context?.User, dataSet)
            ? dataSet
            : null;
    }

    private static bool IsReadable(ConnectedDataSet dataSet)
        => dataSet is { IsEnabled: true, AccessMode: DataSetAccessMode.Imported } && dataSet.Fields?.Count > 0;
}
