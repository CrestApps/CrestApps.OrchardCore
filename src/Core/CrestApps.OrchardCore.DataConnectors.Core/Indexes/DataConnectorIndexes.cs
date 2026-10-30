using CrestApps.Core.Data.YesSql.Indexes;
using CrestApps.OrchardCore.DataConnectors.Models;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.DataConnectors.Indexes;

/// <summary>
/// Indexes data connections by name and connector.
/// </summary>
public sealed class DataConnectionIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the connection's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the connection's display name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the connector's name.
    /// </summary>
    public string Connector { get; set; }
}

/// <summary>
/// Maps data connections to <see cref="DataConnectionIndex"/>.
/// </summary>
public sealed class DataConnectionIndexProvider : IndexProvider<DataConnection>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionIndexProvider"/> class.
    /// </summary>
    public DataConnectionIndexProvider()
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DataConnection> context)
        => context.For<DataConnectionIndex>()
            .Map(connection => new DataConnectionIndex
            {
                ItemId = connection.ItemId,
                Name = connection.Name,
                DisplayText = Truncate(connection.DisplayText),
                Connector = connection.Connector,
            });

    internal static string Truncate(string value)
        => value is { Length: > 255 } ? value[..255] : value;
}

/// <summary>
/// Indexes connected data sets by name, connection, mode and refresh schedule.
/// </summary>
public sealed class ConnectedDataSetIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the data set's technical name.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the data set's display name.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the connection the data set reads.
    /// </summary>
    public string ConnectionId { get; set; }

    /// <summary>
    /// Gets or sets how consumers read the data set.
    /// </summary>
    public DataSetAccessMode AccessMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the data set is enabled.
    /// </summary>
    public bool IsEnabled { get; set; }

    /// <summary>
    /// Gets or sets when the next scheduled refresh is due.
    /// </summary>
    public DateTime? NextRefreshUtc { get; set; }

    /// <summary>
    /// Gets or sets when someone asked for a refresh that hasn't run yet.
    /// </summary>
    public DateTime? RefreshRequestedUtc { get; set; }
}

/// <summary>
/// Maps connected data sets to <see cref="ConnectedDataSetIndex"/>.
/// </summary>
public sealed class ConnectedDataSetIndexProvider : IndexProvider<ConnectedDataSet>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectedDataSetIndexProvider"/> class.
    /// </summary>
    public ConnectedDataSetIndexProvider()
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<ConnectedDataSet> context)
        => context.For<ConnectedDataSetIndex>()
            .Map(dataSet => new ConnectedDataSetIndex
            {
                ItemId = dataSet.ItemId,
                Name = dataSet.Name,
                DisplayText = DataConnectionIndexProvider.Truncate(dataSet.DisplayText),
                ConnectionId = dataSet.ConnectionId,
                AccessMode = dataSet.AccessMode,
                IsEnabled = dataSet.IsEnabled,
                NextRefreshUtc = dataSet.AccessMode == DataSetAccessMode.Imported ? dataSet.Import?.NextRefreshUtc : null,
                RefreshRequestedUtc = dataSet.AccessMode == DataSetAccessMode.Imported ? dataSet.Import?.RefreshRequestedUtc : null,
            });
}

/// <summary>
/// Indexes the refreshes of imported data sets by data set and start.
/// </summary>
public sealed class DataSetRefreshRunIndex : CatalogItemIndex
{
    /// <summary>
    /// Gets or sets the identifier of the data set refreshed.
    /// </summary>
    public string DataSetId { get; set; }

    /// <summary>
    /// Gets or sets how the refresh ended.
    /// </summary>
    public DataSetRefreshStatus Status { get; set; }

    /// <summary>
    /// Gets or sets when the refresh started.
    /// </summary>
    public DateTime StartedUtc { get; set; }
}

/// <summary>
/// Maps refreshes to <see cref="DataSetRefreshRunIndex"/>.
/// </summary>
public sealed class DataSetRefreshRunIndexProvider : IndexProvider<DataSetRefreshRun>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DataSetRefreshRunIndexProvider"/> class.
    /// </summary>
    public DataSetRefreshRunIndexProvider()
    {
        CollectionName = DataConnectorConstants.CollectionName;
    }

    /// <inheritdoc/>
    public override void Describe(DescribeContext<DataSetRefreshRun> context)
        => context.For<DataSetRefreshRunIndex>()
            .Map(run => new DataSetRefreshRunIndex
            {
                ItemId = run.ItemId,
                DataSetId = run.DataSetId,
                Status = run.Status,
                StartedUtc = run.StartedUtc,
            });
}
