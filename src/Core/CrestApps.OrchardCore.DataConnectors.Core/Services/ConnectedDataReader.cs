using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.DataConnectors.Models;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Reads connected data sets from their external systems, through their connections' connectors: checks and lists
/// connections, describes data sets, and reads them live.
/// </summary>
public sealed class ConnectedDataReader
{
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectorManager _connectorManager;
    private readonly IDataConnectionSecretManager _secretManager;
    private readonly DataConnectorsOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataReader"/> class.
    /// </summary>
    /// <param name="connectionStore">The connections.</param>
    /// <param name="connectorManager">The registered connectors.</param>
    /// <param name="secretManager">The secrets of connections.</param>
    /// <param name="options">The limits of reads.</param>
    public ConnectedDataReader(
        IDataConnectionStore connectionStore,
        IDataConnectorManager connectorManager,
        IDataConnectionSecretManager secretManager,
        IOptions<DataConnectorsOptions> options)
    {
        _connectionStore = connectionStore;
        _connectorManager = connectorManager;
        _secretManager = secretManager;
        _options = options.Value;
    }

    /// <summary>
    /// Finds the connection and the connector of a data set.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the query.</param>
    /// <returns>The connection with its connector.</returns>
    /// <exception cref="DataConnectorException">The connection is gone or its connector isn't enabled.</exception>
    public async Task<ConnectedSource> ResolveAsync(ConnectedDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);

        var connection = string.IsNullOrEmpty(dataSet.ConnectionId)
            ? null
            : await _connectionStore.FindByIdAsync(dataSet.ConnectionId, cancellationToken);

        if (connection is null)
        {
            throw new DataConnectorException($"The connection of the data set '{dataSet.Name}' no longer exists.");
        }

        return Resolve(connection);
    }

    /// <summary>
    /// Finds the connector of a connection.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <returns>The connection with its connector.</returns>
    /// <exception cref="DataConnectorException">The connection's connector isn't enabled.</exception>
    public ConnectedSource Resolve(DataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var descriptor = _connectorManager.GetDescriptor(connection.Connector);
        var connector = descriptor is null ? null : _connectorManager.GetConnector(descriptor.Name);

        if (connector is null)
        {
            throw new DataConnectorException($"The connector '{connection.Connector}' of the connection '{connection.Name}' isn't enabled.");
        }

        return new ConnectedSource(connection, descriptor, connector, _secretManager.CreateReader(connection));
    }

    /// <summary>
    /// Checks a connection.
    /// </summary>
    /// <param name="connection">The connection, saved or not.</param>
    /// <param name="cancellationToken">The token that cancels the check.</param>
    /// <returns>Whether the system answered.</returns>
    public async Task<DataConnectorTestResult> TestAsync(DataConnection connection, CancellationToken cancellationToken = default)
    {
        var source = Resolve(connection);

        using var timeout = CreateTimeout(cancellationToken);

        return await source.Connector.TestAsync(new DataConnectorContext(connection, source.Secrets), timeout.Token);
    }

    /// <summary>
    /// Lists the resources of a connection, or none when its connector can't list them.
    /// </summary>
    /// <param name="connection">The connection.</param>
    /// <param name="cancellationToken">The token that cancels the listing.</param>
    /// <returns>The resources.</returns>
    public async Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnection connection, CancellationToken cancellationToken = default)
    {
        var source = Resolve(connection);

        if (!source.Descriptor.Supports(DataConnectorCapabilities.Discovery))
        {
            return [];
        }

        using var timeout = CreateTimeout(cancellationToken);

        return await source.Connector.DiscoverAsync(new DataConnectorContext(connection, source.Secrets), timeout.Token) ?? [];
    }

    /// <summary>
    /// Returns the fields the source of a data set describes now.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the description.</param>
    /// <returns>The source fields.</returns>
    public async Task<IReadOnlyList<DataField>> DescribeAsync(ConnectedDataSet dataSet, CancellationToken cancellationToken = default)
    {
        var source = await ResolveAsync(dataSet, cancellationToken);

        return await DescribeAsync(source, dataSet, cancellationToken);
    }

    /// <summary>
    /// Returns the fields the source of a data set describes now, through a resolved connection.
    /// </summary>
    /// <param name="source">The connection with its connector.</param>
    /// <param name="dataSet">The data set.</param>
    /// <param name="cancellationToken">The token that cancels the description.</param>
    /// <returns>The source fields.</returns>
    public async Task<IReadOnlyList<DataField>> DescribeAsync(ConnectedSource source, ConnectedDataSet dataSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(dataSet);

        using var timeout = CreateTimeout(cancellationToken);

        var context = new DataConnectorReadContext(source.Connection, source.Secrets, dataSet)
        {
            Purpose = DataConnectorReadPurpose.Preview,
        };

        var fields = await source.Connector.DescribeAsync(context, timeout.Token) ?? [];

        return [.. fields.Where(field => field is not null && !string.IsNullOrEmpty(field.Name))];
    }

    /// <summary>
    /// Reads a data set from its source for a consumer, mapped to the data set's fields.
    /// </summary>
    /// <param name="dataSet">The data set.</param>
    /// <param name="query">The consumer's query.</param>
    /// <param name="purpose">Why the rows are read.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The batches of rows.</returns>
    public async IAsyncEnumerable<DataBatch> ReadAsync(
        ConnectedDataSet dataSet,
        DataSourceQuery query,
        DataConnectorReadPurpose purpose,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataSet);
        ArgumentNullException.ThrowIfNull(query);

        var source = await ResolveAsync(dataSet, cancellationToken);
        var mapper = ConnectedDataSetMapper.Create(dataSet, query.Fields);
        var batchSize = query.BatchSize > 0 ? query.BatchSize : DataSourceQuery.DefaultBatchSize;
        var context = new DataConnectorReadContext(source.Connection, source.Secrets, dataSet)
        {
            Purpose = purpose,
            MaxRows = query.MaxRows,
            BatchSize = batchSize,
        };

        foreach (var name in mapper.SourceNames)
        {
            context.Fields.Add(name);
        }

        foreach (var (name, value) in query.Parameters ?? new Dictionary<string, object>())
        {
            context.Parameters[name] = value;
        }

        if (source.Descriptor.Supports(DataConnectorCapabilities.ConditionPushdown))
        {
            foreach (var condition in mapper.ToSourceConditions(query.Conditions))
            {
                context.Conditions.Add(condition);
            }
        }

        using var timeout = CreateTimeout(cancellationToken);

        await foreach (var batch in mapper.MapAsync(source.Connector.ReadAsync(context, timeout.Token), batchSize, query.MaxRows, timeout.Token))
        {
            yield return batch;
        }
    }

    private CancellationTokenSource CreateTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        if (_options.LiveReadTimeout > TimeSpan.Zero)
        {
            timeout.CancelAfter(_options.LiveReadTimeout);
        }

        return timeout;
    }
}

/// <summary>
/// A connection with its connector, ready to read.
/// </summary>
/// <param name="Connection">The connection.</param>
/// <param name="Descriptor">The connector's descriptor.</param>
/// <param name="Connector">The connector.</param>
/// <param name="Secrets">The reader of the connection's secrets.</param>
public sealed record ConnectedSource(
    DataConnection Connection,
    DataConnectorDescriptor Descriptor,
    IDataConnector Connector,
    IDataConnectorSecrets Secrets);
