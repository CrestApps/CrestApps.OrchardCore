using System.Runtime.CompilerServices;
using CrestApps.Core.Models;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.DataConnectors;
using CrestApps.OrchardCore.DataConnectors.Models;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Tests.DataConnectors.AI;

/// <summary>
/// A catalog kept in memory that records what a tool deleted and updated.
/// </summary>
internal class InMemoryCatalog<T> : ICatalog<T>
    where T : CatalogItem
{
    public Dictionary<string, T> Items { get; } = new(StringComparer.Ordinal);

    public List<string> Created { get; } = [];

    public List<string> Updated { get; } = [];

    public List<string> Deleted { get; } = [];

    public void Add(T item)
        => Items[item.ItemId] = item;

    public ValueTask<T> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => ValueTask.FromResult(id is null ? null : Items.GetValueOrDefault(id));

    public ValueTask<IReadOnlyCollection<T>> GetAllAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<T>>([.. Items.Values]);

    public ValueTask<IReadOnlyCollection<T>> GetAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
        => ValueTask.FromResult<IReadOnlyCollection<T>>([.. ids.Where(Items.ContainsKey).Select(id => Items[id])]);

    public ValueTask<PageResult<T>> PageAsync<TQuery>(int page, int pageSize, TQuery context, CancellationToken cancellationToken = default)
        where TQuery : QueryContext
        => throw new NotSupportedException();

    public ValueTask<bool> DeleteAsync(T entry, CancellationToken cancellationToken = default)
    {
        Deleted.Add(entry.ItemId);

        return ValueTask.FromResult(Items.Remove(entry.ItemId));
    }

    public ValueTask CreateAsync(T entry, CancellationToken cancellationToken = default)
    {
        Created.Add(entry.ItemId);
        Items[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }

    public ValueTask UpdateAsync(T entry, CancellationToken cancellationToken = default)
    {
        Updated.Add(entry.ItemId);
        Items[entry.ItemId] = entry;

        return ValueTask.CompletedTask;
    }
}

internal sealed class FakeDataConnectionStore : InMemoryCatalog<DataConnection>, IDataConnectionStore
{
    public Task<DataConnection> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Values.FirstOrDefault(connection => string.Equals(connection.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<DataConnection>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataConnection>>([.. Items.Values.OrderBy(connection => connection.DisplayText, StringComparer.OrdinalIgnoreCase)]);
}

internal sealed class FakeConnectedDataSetStore : InMemoryCatalog<ConnectedDataSet>, IConnectedDataSetStore
{
    public Task<ConnectedDataSet> FindByNameAsync(string name, CancellationToken cancellationToken = default)
        => Task.FromResult(Items.Values.FirstOrDefault(dataSet => string.Equals(dataSet.Name, name, StringComparison.OrdinalIgnoreCase)));

    public Task<IReadOnlyList<ConnectedDataSet>> ListAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConnectedDataSet>>([.. Items.Values.OrderBy(dataSet => dataSet.DisplayText, StringComparer.OrdinalIgnoreCase)]);

    public Task<IReadOnlyList<ConnectedDataSet>> ListByConnectionAsync(string connectionId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConnectedDataSet>>([.. Items.Values.Where(dataSet => dataSet.ConnectionId == connectionId)]);

    public Task<IReadOnlyList<ConnectedDataSet>> ListDueAsync(DateTime utcNow, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ConnectedDataSet>>([]);
}

internal sealed class FakeRefreshRunStore : InMemoryCatalog<DataSetRefreshRun>, IDataSetRefreshRunStore
{
    public List<(string DataSetId, int Keep)> Pruned { get; } = [];

    public Task<(int Count, IReadOnlyList<DataSetRefreshRun> Runs)> PageAsync(string dataSetId, int skip, int take, CancellationToken cancellationToken = default)
    {
        var runs = Items.Values.Where(run => run.DataSetId == dataSetId).OrderByDescending(run => run.StartedUtc).ToList();

        return Task.FromResult<(int, IReadOnlyList<DataSetRefreshRun>)>((runs.Count, [.. runs.Skip(skip).Take(take)]));
    }

    public Task<int> PruneAsync(string dataSetId, int keep, CancellationToken cancellationToken = default)
    {
        Pruned.Add((dataSetId, keep));

        return Task.FromResult(0);
    }
}

/// <summary>
/// The connectors a test enables, all served by one <see cref="TestDataConnector"/>, under the names of the real ones.
/// </summary>
internal sealed class FakeDataConnectorManager : IDataConnectorManager
{
    private readonly List<DataConnectorDescriptor> _descriptors = [];

    public TestDataConnector Connector { get; } = new();

    public FakeDataConnectorManager Add(string name, DataConnectorCapabilities capabilities)
    {
        _descriptors.Add(new DataConnectorDescriptor(
            name,
            LocalizationSource.Create<FakeDataConnectorManager>("A test connector"),
            LocalizationSource.Create<FakeDataConnectorManager>("A connector of a test."),
            capabilities,
            typeof(TestDataConnector)));

        return this;
    }

    public IReadOnlyList<DataConnectorDescriptor> GetConnectors()
        => _descriptors;

    public DataConnectorDescriptor GetDescriptor(string name)
        => _descriptors.FirstOrDefault(descriptor => string.Equals(descriptor.Name, name, StringComparison.OrdinalIgnoreCase));

    public IDataConnector GetConnector(string name)
        => GetDescriptor(name) is null ? null : Connector;
}

/// <summary>
/// A data source that returns the rows a test sets and records who read it.
/// </summary>
internal sealed class FakeDataSource : IDataSource
{
    public static string Name => DataConnectorConstants.LiveDataSourceName;

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<FakeDataSource>("Test data");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<FakeDataSource>("Rows a test sets.");

    public IReadOnlyList<DataField> Fields { get; set; } = [new("Id", "Id", DataFieldType.Integer), new("Name", "Name", DataFieldType.Text)];

    public List<object[]> Rows { get; } = [];

    public List<DataSourceQuery> Queries { get; } = [];

    public Exception Failure { get; set; }

    public Task<IReadOnlyList<DataSetDescriptor>> GetDataSetsAsync(DataSourceContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<DataSetDescriptor>>([]);

    public Task<DataSetSchema> GetSchemaAsync(string dataSet, DataSourceContext context, CancellationToken cancellationToken = default)
        => Task.FromResult<DataSetSchema>(null);

    public async IAsyncEnumerable<DataBatch> ReadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Queries.Add(query);

        await Task.Yield();

        if (Failure is not null)
        {
            throw Failure;
        }

        var rows = query.MaxRows > 0 ? Rows.Take(query.MaxRows) : Rows;

        yield return new DataBatch(Fields, [.. rows]);
    }
}

internal sealed class FakeDataSourceManager : IDataSourceManager
{
    public FakeDataSource Source { get; } = new();

    public IReadOnlyList<DataSourceDescriptor> GetDataSources()
        => [];

    public DataSourceDescriptor GetDescriptor(string name)
        => null;

    public IDataSource GetDataSource(string name)
        => name is DataConnectorConstants.LiveDataSourceName or DataConnectorConstants.ImportedDataSourceName ? Source : null;
}
