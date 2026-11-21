using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Models;
using CrestApps.OrchardCore.DataConnectors.Services;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer;

/// <summary>
/// A SQL Server connection's password is for one server and one way of signing in.
/// </summary>
internal sealed class SqlServerSecretTarget : IDataConnectionSecretTarget
{
    /// <inheritdoc/>
    public string Connector
        => SqlServerDataConnector.Name;

    /// <inheritdoc/>
    public string Describe(DataConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var settings = connection.GetOrCreate<SqlServerConnectionSettings>();

        return Describe(settings.Server, settings.Authentication);
    }

    /// <summary>
    /// Returns the place a password typed for this server and authentication may be sent.
    /// </summary>
    /// <param name="server">The server.</param>
    /// <param name="authentication">The way the connection signs in.</param>
    public static string Describe(string server, SqlServerAuthenticationType authentication)
        => string.Concat(server?.Trim().ToUpperInvariant(), "|", ((int)authentication).ToString(CultureInfo.InvariantCulture));
}
