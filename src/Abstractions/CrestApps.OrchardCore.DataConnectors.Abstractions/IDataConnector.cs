using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Reads an external system, such as a database, a search index, an HTTP API or a file, for the data sets that are
/// configured on one of its connections.
/// </summary>
/// <remarks>
/// A connector only knows how to talk to its system. It doesn't store rows, schedule refreshes or check who may read
/// a data set: the data connectors infrastructure does that for every connector, and exposes the data sets through
/// Orchard Core's <see cref="IDataSource"/> contract. Register a connector with
/// <c>services.AddDataConnector&lt;TConnector&gt;()</c>.
/// <para>
/// A connector returns rows keyed by its own field names (the <see cref="DataField.Name"/> of the fields
/// <see cref="DescribeAsync"/> returns). Mapping them to the names of a data set, coercing the values and keeping
/// only the fields asked for is done by the caller.
/// </para>
/// </remarks>
public interface IDataConnector
{
    /// <summary>
    /// Gets the technical name of the connector, stored on each connection that uses it.
    /// </summary>
    static abstract string Name { get; }

    /// <summary>
    /// Gets the name of the connector shown to users.
    /// </summary>
    static abstract LocalizationSource DisplayName { get; }

    /// <summary>
    /// Gets the description of the connector shown to users.
    /// </summary>
    static abstract LocalizationSource Description { get; }

    /// <summary>
    /// Gets what the connector can do.
    /// </summary>
    static abstract DataConnectorCapabilities Capabilities { get; }

    /// <summary>
    /// Checks that the connection's settings reach the external system.
    /// </summary>
    /// <param name="context">The connection to check.</param>
    /// <param name="cancellationToken">The token that cancels the check.</param>
    /// <returns>Whether the system answered, and a message safe to show.</returns>
    Task<DataConnectorTestResult> TestAsync(DataConnectorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists the resources, such as tables, indices or files, that a data set can read through the connection.
    /// </summary>
    /// <param name="context">The connection to list.</param>
    /// <param name="cancellationToken">The token that cancels the listing.</param>
    /// <returns>The resources, or an empty list when the connector can't list them.</returns>
    Task<IReadOnlyList<DataConnectorResource>> DiscoverAsync(DataConnectorContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the fields of a data set's resource, as the external system describes them or as a sample of its rows
    /// shows them.
    /// </summary>
    /// <param name="context">The data set to describe.</param>
    /// <param name="cancellationToken">The token that cancels the description.</param>
    /// <returns>The source fields, named as <see cref="ReadAsync"/> returns them.</returns>
    Task<IReadOnlyList<DataField>> DescribeAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads the rows of a data set's resource in batches.
    /// </summary>
    /// <param name="context">The data set, the source fields wanted, and the conditions, limits and watermark of the read.</param>
    /// <param name="cancellationToken">The token that cancels the read.</param>
    /// <returns>The batches of rows, keyed by source field names.</returns>
    /// <remarks>
    /// Conditions are hints: the caller filters the rows again. A connector applies a condition at the source only
    /// when doing so can't drop a row that matches it. A watermark, when set, must be applied: an incremental import
    /// relies on it.
    /// </remarks>
    IAsyncEnumerable<DataBatch> ReadAsync(DataConnectorReadContext context, CancellationToken cancellationToken = default);
}
