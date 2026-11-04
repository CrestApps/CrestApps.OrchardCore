using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Lets another module use a <see cref="DataConnection"/> as the endpoint and credentials of its own records, such
/// as an AI data source that reads an Elasticsearch index, instead of storing a second copy of them. It is registered
/// while the Data Connectors feature is on; a consumer takes it as an optional dependency.
/// </summary>
public interface IDataConnectionResolver
{
    /// <summary>
    /// Lists the connections of the given connectors, ordered by display text, for a picker.
    /// </summary>
    /// <param name="connectorNames">The technical names of the connectors (see <see cref="DataConnectorConstants.Connectors"/>).</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connections.</returns>
    Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds a connection and pairs it with the reader of its protected secrets.
    /// </summary>
    /// <param name="connectionId">The connection's identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection and its secrets, or <see langword="null"/> when no connection has this identifier.</returns>
    Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default);
}
