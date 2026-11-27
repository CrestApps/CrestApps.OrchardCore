using System.Net;
using System.Net.Sockets;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.Core.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Npgsql;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;

/// <summary>
/// Holds the PostgreSQL connections a data source opens to the rules the SQL Server data connector and the AI egress
/// guard apply (review 3). A connection string a tenant enters names one server, which is kept off the site's own network
/// unless the host allows a private network (<see cref="AIEgressOptions.AllowPrivateNetwork"/>), and is connected to by
/// the address that was checked, while the server's certificate is still checked against its name. Outside the default
/// tenant, it signs in with its own password: it may not name a file on the host's disk (a password file, client
/// certificate, key or root certificate), and the client never uses the host's Kerberos ticket, Windows account, default
/// password file or default client certificate. The globally configured connection string is the host's, so it is used
/// as it is, and outside the default tenant only for the tables the host approved.
/// </summary>
internal sealed class PostgreSQLConnectionGuard
{
    /// <summary>
    /// The error shown when a data source reads a table through the global connection string where it may not.
    /// </summary>
    public const string NotApprovedError = "The globally configured PostgreSQL connection string is only available on the default tenant, or for a table the host approved for this tenant. Enter this data source's own connection string, or ask the host to approve this table.";

    internal const string InvalidError = "This is not a valid PostgreSQL connection string.";

    internal const string OneServerError = "The connection string must name one server by its host name or address: not a list of servers or a local socket.";

    internal const string OwnNetworkError = "This server is on the site's own network. Use a public server, or ask the host to allow a private network for AI connections.";

    internal const string HostFileError = "The connection string names a file on the site's server (a password file, client certificate, key or root certificate), which is only available on the default tenant.";

    internal const string PasswordRequiredError = "The connection string needs a password. Without one, the client would sign in with the site's server account, which is only available on the default tenant.";

    private readonly bool _isDefaultTenant;
    private readonly bool _allowHostIdentity;
    private readonly bool _isDevelopment;
    private readonly bool _allowPrivateNetwork;
    private readonly List<string> _approvedTables;
    private readonly Func<string, CancellationToken, Task<IPAddress[]>> _resolve;

    /// <summary>
    /// Initializes a new instance of the <see cref="PostgreSQLConnectionGuard"/> class.
    /// </summary>
    /// <param name="shellSettings">The settings of the current tenant.</param>
    /// <param name="options">What the host allows a tenant other than the default one.</param>
    /// <param name="egressOptions">The host's AI egress options.</param>
    /// <param name="hostEnvironment">The host environment, where development allows this machine.</param>
    public PostgreSQLConnectionGuard(
        ShellSettings shellSettings,
        IOptions<PostgreSQLSharedConnectionOptions> options,
        IOptions<AIEgressOptions> egressOptions = null,
        IHostEnvironment hostEnvironment = null)
        : this(shellSettings, options, egressOptions, hostEnvironment, resolve: null)
    {
    }

    internal PostgreSQLConnectionGuard(
        ShellSettings shellSettings,
        IOptions<PostgreSQLSharedConnectionOptions> options,
        IOptions<AIEgressOptions> egressOptions,
        IHostEnvironment hostEnvironment,
        Func<string, CancellationToken, Task<IPAddress[]>> resolve)
    {
        _isDefaultTenant = shellSettings?.IsDefaultShell() == true;
        _allowHostIdentity = _isDefaultTenant || options?.Value?.AllowHostIdentity == true;
        _approvedTables = options?.Value?.SharedConnectionTables ?? [];
        _isDevelopment = hostEnvironment?.IsDevelopment() == true;
        _allowPrivateNetwork = egressOptions?.Value?.AllowPrivateNetwork == true;
        _resolve = resolve ?? Dns.GetHostAddressesAsync;
    }

    /// <summary>
    /// Whether this tenant may read the table through the global connection string. The name is compared whole and by
    /// case, as PostgreSQL compares the quoted name the data source reads.
    /// </summary>
    /// <param name="tableName">The table name, as the data source names it.</param>
    public bool IsApprovedTable(string tableName)
        => _isDefaultTenant || (!string.IsNullOrWhiteSpace(tableName) && _approvedTables.Any(entry =>
            !string.IsNullOrWhiteSpace(entry) &&
            string.Equals(NormalizeTable(entry), NormalizeTable(tableName), StringComparison.Ordinal)));

    /// <summary>
    /// Returns why a connection string a tenant entered may not be saved, or <see langword="null"/>. The server's name is
    /// resolved and checked now, and again before every connection.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    public async Task<string> GetErrorAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (Parse(connectionString, out var builder) is { } error)
        {
            return error;
        }

        return await ResolveAllowedAsync(builder.Host.Trim(), cancellationToken) is null ? OwnNetworkError : null;
    }

    /// <summary>
    /// Creates the data source a connection string a tenant entered connects through: to the address that was checked
    /// now, with the server's certificate checked against its name.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The data source. The caller disposes it.</returns>
    /// <exception cref="InvalidOperationException">The connection string breaks a rule, or its server is on the site's own network.</exception>
    public async Task<NpgsqlDataSource> CreateDataSourceAsync(string connectionString, CancellationToken cancellationToken = default)
    {
        if (Parse(connectionString, out var builder) is { } error)
        {
            throw new InvalidOperationException(error);
        }

        var host = builder.Host.Trim();
        var address = await ResolveAllowedAsync(host, cancellationToken) ?? throw new InvalidOperationException(OwnNetworkError);

        return Pin(builder, host, address, _allowHostIdentity).Build();
    }

    /// <summary>
    /// Builds a data source that connects to the checked address instead of resolving the name again, and still checks
    /// the server's certificate against the name. Outside the host's identity, no client certificate is sent.
    /// </summary>
    /// <param name="builder">The checked connection string.</param>
    /// <param name="host">The server's name, as the connection string gives it.</param>
    /// <param name="address">The checked address.</param>
    /// <param name="allowHostIdentity">Whether the connection may use the host's identity.</param>
    /// <returns>The data source builder.</returns>
    internal static NpgsqlDataSourceBuilder Pin(NpgsqlConnectionStringBuilder builder, string host, IPAddress address, bool allowHostIdentity)
    {
        builder.Host = address.ToString();

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(builder.ConnectionString);

        dataSourceBuilder.UseSslClientAuthenticationOptionsCallback(options =>
        {
            options.TargetHost = host;

            if (!allowHostIdentity)
            {
                options.ClientCertificateContext = null;
            }
        });

        return dataSourceBuilder;
    }

    /// <summary>
    /// Parses a connection string a tenant entered and applies the rules that need no network.
    /// </summary>
    /// <param name="connectionString">The connection string.</param>
    /// <param name="builder">The parsed connection string, held to the rules.</param>
    /// <returns>The error, or <see langword="null"/>.</returns>
    internal string Parse(string connectionString, out NpgsqlConnectionStringBuilder builder)
    {
        builder = null;

        try
        {
            builder = new NpgsqlConnectionStringBuilder(connectionString);
        }
        catch (Exception exception) when (exception is ArgumentException or FormatException or InvalidCastException or OverflowException)
        {
            return InvalidError;
        }

        var host = builder.Host?.Trim();

        // A list of servers, a host:port pair or a local socket path would be connected to by a name or path the check
        // never saw.
        if (string.IsNullOrEmpty(host) ||
            host.Contains(',', StringComparison.Ordinal) ||
            host[0] == '@' ||
            Path.IsPathRooted(host) ||
            (host.Contains(':', StringComparison.Ordinal) && !IPAddress.TryParse(host.Trim('[', ']'), out _)))
        {
            return OneServerError;
        }

        if (_allowHostIdentity)
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(builder.Passfile) ||
            !string.IsNullOrWhiteSpace(builder.SslCertificate) ||
            !string.IsNullOrWhiteSpace(builder.SslKey) ||
            !string.IsNullOrWhiteSpace(builder.SslPassword) ||
            !string.IsNullOrWhiteSpace(builder.RootCertificate))
        {
            return HostFileError;
        }

        if (string.IsNullOrEmpty(builder.Password))
        {
            return PasswordRequiredError;
        }

        // Never the host's Kerberos ticket or Windows account, however the server asks to authenticate.
        builder.GssEncryptionMode = GssEncryptionMode.Disable;
        builder.RequireAuth = "!GSS,!SSPI";

        return null;
    }

    // The first address of the server the rule allows, or null when it has none. Allowing a private network never opens
    // the link-local range or a cloud metadata service.
    private async Task<IPAddress> ResolveAllowedAsync(string host, CancellationToken cancellationToken)
    {
        var trimmed = host.Trim('[', ']');
        IPAddress[] addresses;

        try
        {
            addresses = IPAddress.TryParse(trimmed, out var literal)
                ? [literal]
                : await _resolve(trimmed, cancellationToken);
        }
        catch (SocketException)
        {
            return null;
        }

        return addresses?.FirstOrDefault(address => !EgressNetwork.IsRefused(address, _isDevelopment, _allowPrivateNetwork));
    }

    private static string NormalizeTable(string tableName)
        => string.Join('.', tableName.Split('.', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries));
}
