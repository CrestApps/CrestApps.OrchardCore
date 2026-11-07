using CrestApps.OrchardCore.DataConnectors.Models;

namespace CrestApps.OrchardCore.DataConnectors.Services;

/// <summary>
/// Resolves data connections for other modules from the connection store, with a reader of their protected secrets.
/// </summary>
internal sealed class DataConnectionResolver : IDataConnectionResolver
{
    private readonly IDataConnectionStore _connectionStore;
    private readonly IDataConnectionSecretManager _secretManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="DataConnectionResolver"/> class.
    /// </summary>
    /// <param name="connectionStore">The connection store.</param>
    /// <param name="secretManager">The manager that reads the connections' protected secrets.</param>
    public DataConnectionResolver(IDataConnectionStore connectionStore, IDataConnectionSecretManager secretManager)
    {
        _connectionStore = connectionStore;
        _secretManager = secretManager;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<DataConnection>> ListAsync(IEnumerable<string> connectorNames, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectorNames);

        var names = new HashSet<string>(connectorNames.Where(name => !string.IsNullOrEmpty(name)), StringComparer.OrdinalIgnoreCase);

        if (names.Count == 0)
        {
            return [];
        }

        return (await _connectionStore.ListAsync(cancellationToken))
            .Where(connection => connection.Connector is not null && names.Contains(connection.Connector))
            .OrderBy(connection => string.IsNullOrWhiteSpace(connection.DisplayText) ? connection.Name : connection.DisplayText, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    /// <inheritdoc/>
    public async Task<DataConnectorContext> ResolveAsync(string connectionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(connectionId))
        {
            return null;
        }

        var connection = await _connectionStore.FindByIdAsync(connectionId, cancellationToken);

        return connection is null
            ? null
            : new DataConnectorContext(connection, _secretManager.CreateReader(connection));
    }
}
