using System.Data.Common;
using CrestApps.Core;
using CrestApps.OrchardCore.DataConnectors.Http;
using CrestApps.OrchardCore.DataConnectors.Relational;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using OrchardCore.DataSources;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.DataConnectors.SqlServer;

/// <summary>
/// How a connection reaches a SQL Server database. The password, or the client secret of a service principal, is
/// stored apart, protected, as the <see cref="SqlServerDataConnector.PasswordSecret"/> secret.
/// </summary>
public sealed class SqlServerConnectionSettings
{
    /// <summary>
    /// Gets or sets the server: a host name, <c>host,port</c> or <c>host\instance</c>.
    /// </summary>
    public string Server { get; set; }

    /// <summary>
    /// Gets or sets the database.
    /// </summary>
    public string Database { get; set; }

    /// <summary>
    /// Gets or sets how the connection signs in.
    /// </summary>
    public SqlServerAuthenticationType Authentication { get; set; }

    /// <summary>
    /// Gets or sets the login, or the client identifier of a service principal or user-assigned managed identity.
    /// </summary>
    public string UserName { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the traffic is encrypted.
    /// </summary>
    public bool Encrypt { get; set; } = true;

    /// <summary>
    /// Gets or sets a value indicating whether the server's certificate is trusted without being validated.
    /// </summary>
    public bool TrustServerCertificate { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the connection asks for a read-only replica when one is available.
    /// </summary>
    public bool ReadOnlyIntent { get; set; }

    /// <summary>
    /// Gets or sets the seconds to wait for the connection to open.
    /// </summary>
    public int ConnectTimeoutSeconds { get; set; } = 15;
}

/// <summary>
/// How a connection signs in to SQL Server.
/// </summary>
public enum SqlServerAuthenticationType
{
    /// <summary>
    /// A SQL Server login and password.
    /// </summary>
    SqlLogin,

    /// <summary>
    /// The Windows account the site runs as.
    /// </summary>
    Integrated,

    /// <summary>
    /// A Microsoft Entra service principal: its client identifier and secret.
    /// </summary>
    EntraServicePrincipal,

    /// <summary>
    /// The managed identity of the site's host, optionally a user-assigned one by client identifier.
    /// </summary>
    EntraManagedIdentity,

    /// <summary>
    /// The default Microsoft Entra credential chain of the site's host.
    /// </summary>
    EntraDefault,
}

/// <summary>
/// Reads the tables, views and read-only queries of a SQL Server or Azure SQL database.
/// </summary>
internal sealed class SqlServerDataConnector : RelationalDataConnector, IDataConnector
{
    /// <summary>
    /// The name of the secret that holds the password or client secret.
    /// </summary>
    public const string PasswordSecret = "Password";

    private readonly DataConnectorNetworkOptions _networkOptions;

    public SqlServerDataConnector(IOptions<DataConnectorNetworkOptions> networkOptions)
    {
        _networkOptions = networkOptions.Value;
    }

    public static string Name => "SqlServer";

    public static LocalizationSource DisplayName { get; } = LocalizationSource.Create<SqlServerDataConnector>("SQL Server");

    public static LocalizationSource Description { get; } = LocalizationSource.Create<SqlServerDataConnector>("Reads the tables, views and read-only queries of any SQL Server or Azure SQL database.");

    public static DataConnectorCapabilities Capabilities =>
        DataConnectorCapabilities.Live |
        DataConnectorCapabilities.Import |
        DataConnectorCapabilities.IncrementalImport |
        DataConnectorCapabilities.Discovery |
        DataConnectorCapabilities.ConditionPushdown;

    protected override string ListResourcesSql =>
        "SELECT TABLE_SCHEMA, TABLE_NAME, CASE TABLE_TYPE WHEN 'VIEW' THEN 'View' ELSE 'Table' END " +
        "FROM INFORMATION_SCHEMA.TABLES ORDER BY TABLE_SCHEMA, TABLE_NAME";

    protected override string VersionSql => "SELECT @@VERSION";

    /// <summary>
    /// Builds the connection string of a connection. The password never comes from the settings, only from the secret.
    /// </summary>
    internal static SqlConnectionStringBuilder BuildConnectionString(SqlServerConnectionSettings settings, string password)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrWhiteSpace(settings.Server))
        {
            throw new DataConnectorException("The connection names no server.");
        }

        var builder = new SqlConnectionStringBuilder
        {
            DataSource = settings.Server.Trim(),
            Encrypt = settings.Encrypt ? SqlConnectionEncryptOption.Mandatory : SqlConnectionEncryptOption.Optional,
            TrustServerCertificate = settings.TrustServerCertificate,
            ApplicationIntent = settings.ReadOnlyIntent ? ApplicationIntent.ReadOnly : ApplicationIntent.ReadWrite,
            ConnectTimeout = Math.Clamp(settings.ConnectTimeoutSeconds, 1, 300),
            ApplicationName = "CrestApps Data Connectors",
            PersistSecurityInfo = false,
        };

        if (!string.IsNullOrWhiteSpace(settings.Database))
        {
            builder.InitialCatalog = settings.Database.Trim();
        }

        switch (settings.Authentication)
        {
            case SqlServerAuthenticationType.Integrated:
                builder.IntegratedSecurity = true;
                break;

            case SqlServerAuthenticationType.EntraServicePrincipal:
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryServicePrincipal;
                builder.UserID = settings.UserName?.Trim();
                builder.Password = password ?? string.Empty;
                break;

            case SqlServerAuthenticationType.EntraManagedIdentity:
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryManagedIdentity;

                if (!string.IsNullOrWhiteSpace(settings.UserName))
                {
                    builder.UserID = settings.UserName.Trim();
                }

                break;

            case SqlServerAuthenticationType.EntraDefault:
                builder.Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault;
                break;

            default:
                builder.UserID = settings.UserName?.Trim();
                builder.Password = password ?? string.Empty;
                break;
        }

        return builder;
    }

    /// <summary>
    /// Returns the host of a server setting: without the <c>tcp:</c> prefix, the port and the instance.
    /// </summary>
    internal static string GetHost(string server)
    {
        var host = server?.Trim() ?? string.Empty;

        if (host.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            host = host[4..];
        }

        if (host.StartsWith("(localdb)", StringComparison.OrdinalIgnoreCase) || host is "." or "(local)")
        {
            return "localhost";
        }

        var end = host.IndexOfAny([',', '\\']);

        return end < 0 ? host : host[..end];
    }

    protected override async Task<DbConnection> OpenConnectionAsync(DataConnectorContext context, CancellationToken cancellationToken)
    {
        var settings = context.Connection.GetOrCreate<SqlServerConnectionSettings>();
        var builder = BuildConnectionString(settings, context.Secrets.Get(PasswordSecret));

        // The server goes through the same network rule as any other connector, so a connection can't reach the
        // site's own network unless the configuration allows it.
        await DataConnectorNetwork.ResolveAllowedAsync(GetHost(settings.Server), _networkOptions, cancellationToken);

        var connection = new SqlConnection(builder.ConnectionString);

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();

            throw;
        }

        return connection;
    }

    protected override string QuoteIdentifier(string identifier)
        => "[" + (identifier ?? string.Empty).Replace("]", "]]", StringComparison.Ordinal) + "]";

    protected override string Select(string columns, string fromAndWhere, int maxRows)
        => maxRows > 0
            ? $"SELECT TOP ({maxRows}) {columns} FROM {fromAndWhere}"
            : $"SELECT {columns} FROM {fromAndWhere}";

    protected override bool IsTransient(DbException exception)
        => exception is SqlException sql
            ? sql.Number is -2 or 1205 or 4060 or 40197 or 40501 or 40613 or 49918 or 49919 or 49920
            : base.IsTransient(exception);
}
