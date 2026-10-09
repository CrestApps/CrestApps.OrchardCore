using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Runs the SQL Server provisioner against a real server. Set <c>TENANT_HIERARCHY_SQLSERVER</c> to the connection string
/// of a login that can create databases, schemas and logins, for example a disposable container; otherwise the tests are
/// skipped.
/// </summary>
public sealed class SqlServerProvisioningTests
{
    private const string ConnectionStringVariable = "TENANT_HIERARCHY_SQLSERVER";

    private static readonly DateTime _now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string _adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

    [Fact]
    public async Task DatabasePerChild_CreatesADatabaseOnlyItsOwnLoginCanOpen()
    {
        // Arrange
        var provisioner = new SqlServerChildDatabaseProvisioner(new FixedClock(_now));
        var first = CreateContext(ChildDatabaseStrategy.DatabasePerChild);
        var second = CreateContext(ChildDatabaseStrategy.DatabasePerChild);

        // Act
        var firstResult = await provisioner.ProvisionAsync(first, TestContext.Current.CancellationToken);
        var secondResult = await provisioner.ProvisionAsync(second, TestContext.Current.CancellationToken);

        try
        {
            // Assert
            await ExecuteAsync(firstResult.ConnectionString, "CREATE TABLE notes (id int); INSERT INTO notes VALUES (1);");
            Assert.Equal(1, await ScalarAsync(firstResult.ConnectionString, "SELECT COUNT(*) FROM notes;"));

            var crossed = new SqlConnectionStringBuilder(secondResult.ConnectionString)
            {
                InitialCatalog = new SqlConnectionStringBuilder(firstResult.ConnectionString).InitialCatalog,
            };

            var exception = await Record.ExceptionAsync(() => ExecuteAsync(crossed.ConnectionString, "SELECT 1;"));
            Assert.IsType<SqlException>(exception);
        }
        finally
        {
            first.ProvisionedResource = firstResult.ProvisionedResource;
            second.ProvisionedResource = secondResult.ProvisionedResource;
            first.Pool.RetainRemovedDatabases = false;
            second.Pool.RetainRemovedDatabases = false;
            SqlConnection.ClearAllPools();
            await provisioner.DeprovisionAsync(first, TestContext.Current.CancellationToken);
            await provisioner.DeprovisionAsync(second, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DatabasePerChild_RemovalRetainsTheDatabaseUnderANewNameAndDropsTheLogin()
    {
        // Arrange
        var provisioner = new SqlServerChildDatabaseProvisioner(new FixedClock(_now));
        var context = CreateContext(ChildDatabaseStrategy.DatabasePerChild);
        var result = await provisioner.ProvisionAsync(context, TestContext.Current.CancellationToken);
        context.ProvisionedResource = result.ProvisionedResource;

        // Act
        await provisioner.DeprovisionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var retainedName = ProvisioningNames.ForRetainedDatabase(result.ProvisionedResource, _now);
        Assert.Equal(1, await ScalarAsync(_adminConnectionString, $"SELECT COUNT(*) FROM sys.databases WHERE name = N'{retainedName}';"));
        Assert.Equal(0, await ScalarAsync(_adminConnectionString, $"SELECT COUNT(*) FROM sys.databases WHERE name = N'{result.ProvisionedResource}';"));
        Assert.Equal(0, await ScalarAsync(_adminConnectionString, $"SELECT COUNT(*) FROM sys.server_principals WHERE name = N'{result.ProvisionedResource}';"));

        await ExecuteAsync(_adminConnectionString, $"DROP DATABASE [{retainedName}];");
    }

    [Fact]
    public async Task SchemaPerChild_CreatesASchemaOnlyItsOwnLoginCanWriteTo()
    {
        // Arrange
        var provisioner = new SqlServerChildDatabaseProvisioner(new FixedClock(_now));
        var first = CreateContext(ChildDatabaseStrategy.SchemaPerChild);
        var second = CreateContext(ChildDatabaseStrategy.SchemaPerChild);

        // Act
        var firstResult = await provisioner.ProvisionAsync(first, TestContext.Current.CancellationToken);
        var secondResult = await provisioner.ProvisionAsync(second, TestContext.Current.CancellationToken);

        try
        {
            // Assert: the child names the database of its schema, even when the pool's connection string does not.
            Assert.False(string.IsNullOrEmpty(new SqlConnectionStringBuilder(firstResult.ConnectionString).InitialCatalog));
            await ExecuteAsync(firstResult.ConnectionString, $"CREATE TABLE [{firstResult.Schema}].[notes] (id int);");
            var exception = await Record.ExceptionAsync(() => ExecuteAsync(secondResult.ConnectionString, $"CREATE TABLE [{firstResult.Schema}].[stolen] (id int);"));
            Assert.IsType<SqlException>(exception);

            await ExecuteAsync(firstResult.ConnectionString, $"DROP TABLE [{firstResult.Schema}].[notes];");
        }
        finally
        {
            first.ProvisionedResource = firstResult.ProvisionedResource;
            second.ProvisionedResource = secondResult.ProvisionedResource;
            SqlConnection.ClearAllPools();
            await provisioner.DeprovisionAsync(first, TestContext.Current.CancellationToken);
            await provisioner.DeprovisionAsync(second, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0, await ScalarAsync(_adminConnectionString, $"SELECT COUNT(*) FROM sys.schemas WHERE name = N'{firstResult.Schema}';"));
    }

    [Fact]
    public async Task ParentWithDatabasePerChild_SetsUpAChildInItsOwnSqlServerDatabase()
    {
        // Arrange
        RequireServer();

        await using var host = await TenantHierarchyTestHost.StartAsync(new Dictionary<string, string>
        {
            ["TenantHierarchy:DatabasePools:sql:DatabaseProvider"] = "SqlConnection",
            ["TenantHierarchy:DatabasePools:sql:ConnectionString"] = _adminConnectionString,
            ["TenantHierarchy:DatabasePools:sql:RetainRemovedDatabases"] = "false",
        });

        var password = $"Th-{Guid.NewGuid():N}!aA1";
        await host.SetupTenantAsync(ShellSettings.DefaultShellName, "Blank", "platform", password);
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var platform = (await features.GetAvailableFeaturesAsync()).Single(feature => feature.Id == TenantHierarchyConstants.Features.Platform);
            await features.EnableFeaturesAsync([platform], force: true);
        });

        await host.CreateTenantAsync("sqlfirm", "sqlfirm.localhost");
        await host.SetupTenantAsync("sqlfirm", "Blank", "owner", password);
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var (result, errors) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync("sqlfirm", "sqlfirm", "SQL Firm", new ParentTenantPolicy
            {
                DatabaseStrategy = ChildDatabaseStrategy.DatabasePerChild,
                DatabasePool = "sql",
            });

            Assert.True(result.Succeeded, $"{result.Error} {string.Join(' ', errors.Values)}");
        });

        // Act
        var entry = await host.InTenantAsync("sqlfirm", async services =>
        {
            var (result, created) = await services.GetRequiredService<ChildTenantManager>().CreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "SQL Business",
                Slug = "sql-business",
                RecipeName = "Blank",
            });

            Assert.True(result.Succeeded, result.Error);

            return created;
        });

        await host.InTenantAsync("sqlfirm", async services =>
        {
            var result = await services.GetRequiredService<ChildTenantManager>().SetupAsync(entry.EntryId);
            Assert.True(result.Succeeded, result.Error);
        });

        // Assert
        var child = host.GetSettings(entry.TenantName);
        Assert.True(child.IsRunning());
        Assert.Equal("SqlConnection", child["DatabaseProvider"]);
        Assert.Equal(ProvisioningNames.ForTenant(entry.TenantName), new SqlConnectionStringBuilder(child["ConnectionString"]).InitialCatalog);
        Assert.True(await ScalarAsync(child["ConnectionString"], "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_NAME = 'Document';") >= 1);

        await host.InTenantAsync("sqlfirm", async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            Assert.True((await manager.SuspendAsync(entry.EntryId)).Succeeded);
            SqlConnection.ClearAllPools();
            var removal = await manager.RemoveAsync(entry.EntryId);
            Assert.True(removal.Succeeded, removal.Error);
        });

        Assert.Equal(0, await ScalarAsync(_adminConnectionString, $"SELECT COUNT(*) FROM sys.databases WHERE name = N'{ProvisioningNames.ForTenant(entry.TenantName)}';"));
    }

    private static ChildDatabaseProvisioningContext CreateContext(ChildDatabaseStrategy strategy)
    {
        RequireServer();

        return new ChildDatabaseProvisioningContext
        {
            TenantName = TenantHierarchyNaming.GenerateTenantName(),
            Strategy = strategy,
            PoolName = "sql",
            Pool = new DatabasePoolOptions
            {
                DatabaseProvider = DatabaseProviderNames.SqlServer,
                ConnectionString = _adminConnectionString,
            },
        };
    }

    private static void RequireServer()
    {
        if (string.IsNullOrEmpty(_adminConnectionString))
        {
            Assert.Skip($"Set {ConnectionStringVariable} to run the SQL Server provisioning tests.");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<int> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);

        return Convert.ToInt32(await command.ExecuteScalarAsync());
    }

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; }

        public ITimeZone[] GetTimeZones() => throw new NotSupportedException();

        public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => throw new NotSupportedException();
    }
}
