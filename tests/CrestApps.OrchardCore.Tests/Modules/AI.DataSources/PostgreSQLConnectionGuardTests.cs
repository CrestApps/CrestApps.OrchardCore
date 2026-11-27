using System.Net;
using System.Net.Sockets;
using CrestApps.Core;
using CrestApps.Core.AI.DataSources;
using CrestApps.Core.AI.Models;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Models;
using CrestApps.OrchardCore.AI.DataSources.PostgreSQL.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Npgsql;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Modules.AI.DataSources;

/// <summary>
/// Review 3 (Medium, AI): the PostgreSQL data source opened the raw connection string a tenant's administrator typed,
/// with no egress check and no keyword filter, so it could reach the site's own network, read files on the host's disk
/// (a password file or client certificate) or sign in as the host. The connection string is now held to the AI egress
/// rule, connected to by the checked address, and, outside the default tenant, may not sign in as the host. Names
/// resolve over a fixed answer: <c>inside.example.com</c> is on the site's own network.
/// </summary>
public sealed class PostgreSQLConnectionGuardTests
{
    private const string Tenant = "Customer1";

    private readonly EphemeralDataProtectionProvider _protection = new();

    [Theory]
    [InlineData("Host=10.0.0.5;Username=reader;Password=secret")]
    [InlineData("Host=127.0.0.1;Username=reader;Password=secret")]
    [InlineData("Host=169.254.169.254;Username=reader;Password=secret")]
    [InlineData("Host=inside.example.com;Username=reader;Password=secret")]
    [InlineData("Host=missing.example.com;Username=reader;Password=secret")]
    public async Task GetErrorAsync_AServerOnTheSiteOwnNetwork_IsRefused(string connectionString)
    {
        // Arrange
        var guard = CreateGuard(ShellSettings.DefaultShellName);

        // Act
        var error = await guard.GetErrorAsync(connectionString, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PostgreSQLConnectionGuard.OwnNetworkError, error);
    }

    [Theory]
    [InlineData("Host=db.example.com,inside.example.com;Username=reader;Password=secret")]
    [InlineData("Host=/var/run/postgresql;Username=reader;Password=secret")]
    [InlineData("Host=@pgsocket;Username=reader;Password=secret")]
    [InlineData("Host=db.example.com:5433;Username=reader;Password=secret")]
    public async Task GetErrorAsync_MoreThanOneServerOrALocalSocket_IsRefused(string connectionString)
    {
        // Arrange
        var guard = CreateGuard(ShellSettings.DefaultShellName);

        // Act
        var error = await guard.GetErrorAsync(connectionString, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(PostgreSQLConnectionGuard.OneServerError, error);
    }

    [Theory]
    [InlineData("Host=db.example.com;Username=reader;Password=secret;Passfile=C:\\secrets\\pgpass.conf", PostgreSQLConnectionGuard.HostFileError)]
    [InlineData("Host=db.example.com;Username=reader;Password=secret;SSL Certificate=/etc/ssl/client.crt", PostgreSQLConnectionGuard.HostFileError)]
    [InlineData("Host=db.example.com;Username=reader;Password=secret;SSL Key=/etc/ssl/client.key", PostgreSQLConnectionGuard.HostFileError)]
    [InlineData("Host=db.example.com;Username=reader;Password=secret;Root Certificate=/etc/ssl/ca.crt", PostgreSQLConnectionGuard.HostFileError)]
    [InlineData("Host=db.example.com;Username=reader", PostgreSQLConnectionGuard.PasswordRequiredError)]
    public async Task GetErrorAsync_OnAnotherTenant_ASignInAsTheHost_IsRefused(string connectionString, string expected)
    {
        // Arrange
        var guard = CreateGuard(Tenant);
        var defaultTenantGuard = CreateGuard(ShellSettings.DefaultShellName);

        // Act
        var error = await guard.GetErrorAsync(connectionString, TestContext.Current.CancellationToken);
        var defaultTenantError = await defaultTenantGuard.GetErrorAsync(connectionString, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, error);
        Assert.Null(defaultTenantError);
    }

    [Fact]
    public void Parse_OnAnotherTenant_NeverUsesTheHostsKerberosTicketOrWindowsAccount()
    {
        // Arrange
        var guard = CreateGuard(Tenant);

        // Act
        var error = guard.Parse("Host=db.example.com;Username=reader;Password=secret;GSS Encryption Mode=Require;Require Auth=GSS", out var builder);

        // Assert
        Assert.Null(error);
        Assert.Equal(GssEncryptionMode.Disable, builder.GssEncryptionMode);
        Assert.Equal("!GSS,!SSPI", builder.RequireAuth);
    }

    [Fact]
    public void Pin_ConnectsToTheCheckedAddress()
    {
        // Arrange
        var builder = new NpgsqlConnectionStringBuilder("Host=db.example.com;Username=reader;Password=secret;SSL Mode=VerifyFull");

        // Act
        var dataSourceBuilder = PostgreSQLConnectionGuard.Pin(builder, "db.example.com", IPAddress.Parse("203.0.113.10"), allowHostIdentity: false);

        // Assert
        Assert.Equal("203.0.113.10", new NpgsqlConnectionStringBuilder(dataSourceBuilder.ConnectionString).Host);
    }

    [Fact]
    public async Task ReadAsync_AConnectionStringToTheSiteOwnNetwork_NeverConnects()
    {
        // Arrange
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var handler = new PostgreSQLAIDataSourceSourceHandler(
            _protection,
            Options.Create(new PostgreSQLDataSourceOptions()),
            NullLogger<PostgreSQLAIDataSourceSourceHandler>.Instance,
            CreateGuard(ShellSettings.DefaultShellName));
        var dataSource = Source($"Host=127.0.0.1;Port={port};Username=reader;Password=secret;Timeout=2");

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var _ in handler.ReadAsync(dataSource, TestContext.Current.CancellationToken))
            {
            }
        });

        // Assert
        Assert.Equal(PostgreSQLConnectionGuard.OwnNetworkError, exception.Message);
        Assert.False(listener.Pending());
    }

    [Fact]
    public async Task ValidateAsync_AConnectionStringToTheSiteOwnNetwork_IsRefused()
    {
        // Arrange
        var handler = new PostgreSQLAIDataSourceSourceHandler(
            _protection,
            Options.Create(new PostgreSQLDataSourceOptions()),
            NullLogger<PostgreSQLAIDataSourceSourceHandler>.Instance,
            CreateGuard(ShellSettings.DefaultShellName));
        var result = new ValidationResultDetails();

        // Act
        await handler.ValidateAsync(Source("Host=10.0.0.5;Username=reader;Password=secret"), result, TestContext.Current.CancellationToken);

        // Assert
        Assert.Contains(result.Errors, error => error.MemberNames.Contains(nameof(PostgreSQLSourceMetadata.ConnectionString)) && error.ErrorMessage == PostgreSQLConnectionGuard.OwnNetworkError);
    }

    private AIDataSource Source(string connectionString)
    {
        var dataSource = new AIDataSource { ItemId = "ds-pg", Source = AIDataSourceSourceTypes.PostgreSQL, DisplayText = "Articles" };
        dataSource.Put(new PostgreSQLSourceMetadata
        {
            TableName = "public.articles",
            ConnectionString = _protection.CreateProtector(AIDataSourceProtectionConstants.SourceSecretPurpose).Protect(connectionString),
        });

        return dataSource;
    }

    private static PostgreSQLConnectionGuard CreateGuard(string tenant)
        => new(
            new ShellSettings { Name = tenant },
            Options.Create(new PostgreSQLSharedConnectionOptions()),
            Options.Create(new AIEgressOptions()),
            hostEnvironment: null,
            Resolve);

    private static Task<IPAddress[]> Resolve(string host, CancellationToken cancellationToken)
        => host switch
        {
            "inside.example.com" => Task.FromResult<IPAddress[]>([IPAddress.Parse("10.0.0.9")]),
            "missing.example.com" => Task.FromException<IPAddress[]>(new SocketException((int)SocketError.HostNotFound)),
            _ => Task.FromResult<IPAddress[]>([IPAddress.Parse("203.0.113.10")]),
        };
}
