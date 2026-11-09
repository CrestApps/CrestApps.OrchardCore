namespace CrestApps.OrchardCore.DataConnectors;

/// <summary>
/// Helpers for the consumers of <see cref="IDataConnectionResolver"/>.
/// </summary>
public static class DataConnectionResolverExtensions
{
    /// <summary>
    /// Resolves a referenced connection and checks that it belongs to one of the connectors the consumer can read. The
    /// resolver may be <see langword="null"/>: a consumer takes it as an optional dependency, and it is missing while the
    /// Data Connectors feature is off.
    /// </summary>
    /// <param name="resolver">The resolver, or <see langword="null"/>.</param>
    /// <param name="connectionId">The connection's identifier.</param>
    /// <param name="connectorNames">The technical names of the connectors the consumer accepts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection with its secrets, or why it cannot be used.</returns>
    public static async Task<DataConnectionResolution> ResolveForAsync(
        this IDataConnectionResolver resolver,
        string connectionId,
        IReadOnlyCollection<string> connectorNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(connectorNames);

        if (resolver is null)
        {
            return new DataConnectionResolution(null, "The record uses a data connection, but the Data Connectors feature is not enabled.");
        }

        var context = await resolver.ResolveAsync(connectionId, cancellationToken);

        if (context is null)
        {
            return new DataConnectionResolution(null, "The data connection the record uses no longer exists.");
        }

        return connectorNames.Contains(context.Connection.Connector, StringComparer.OrdinalIgnoreCase)
            ? new DataConnectionResolution(context, null)
            : new DataConnectionResolution(null, $"The data connection '{context.Connection.Name}' is a {context.Connection.Connector} connection, which this record cannot use.");
    }
}
