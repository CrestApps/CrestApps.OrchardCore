using CrestApps.OrchardCore.TenantHierarchy.Core.Provisioning;
using CrestApps.OrchardCore.TenantHierarchy.Core.Services;
using CrestApps.OrchardCore.TenantHierarchy.Models;
using CrestApps.OrchardCore.TenantHierarchy.Services;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.TenantHierarchy.IntegrationTests;

/// <summary>
/// Runs the PostgreSQL provisioner against a real server. Set <c>TENANT_HIERARCHY_POSTGRES</c> to the connection string
/// of a login that can create databases and roles, for example a disposable container; otherwise the tests are skipped.
/// </summary>
public sealed class PostgreSqlProvisioningTests
{
    private const string ConnectionStringVariable = "TENANT_HIERARCHY_POSTGRES";

    private static readonly string _adminConnectionString = Environment.GetEnvironmentVariable(ConnectionStringVariable);

    [Fact]
    public async Task DatabasePerChild_CreatesADatabaseOnlyItsOwnRoleCanReach()
    {
        // Arrange
        var provisioner = new PostgreSqlChildDatabaseProvisioner(new TestClock());
        var first = CreateContext(ChildDatabaseStrategy.DatabasePerChild);
        var second = CreateContext(ChildDatabaseStrategy.DatabasePerChild);

        // Act
        var firstResult = await provisioner.ProvisionAsync(first, TestContext.Current.CancellationToken);
        var secondResult = await provisioner.ProvisionAsync(second, TestContext.Current.CancellationToken);

        try
        {
            // Assert: each role works in its own database.
            await ExecuteAsync(firstResult.ConnectionString, "CREATE TABLE notes (id int); INSERT INTO notes VALUES (1);");
            Assert.Equal(1L, await ScalarAsync(firstResult.ConnectionString, "SELECT count(*) FROM notes;"));

            // The second child's role cannot connect to the first child's database.
            var crossed = new NpgsqlConnectionStringBuilder(secondResult.ConnectionString)
            {
                Database = new NpgsqlConnectionStringBuilder(firstResult.ConnectionString).Database,
            };

            var exception = await Record.ExceptionAsync(() => ExecuteAsync(crossed.ConnectionString, "SELECT 1;"));
            Assert.IsType<PostgresException>(exception);
        }
        finally
        {
            first.ProvisionedResource = firstResult.ProvisionedResource;
            second.ProvisionedResource = secondResult.ProvisionedResource;
            first.Pool.RetainRemovedDatabases = false;
            second.Pool.RetainRemovedDatabases = false;
            await provisioner.DeprovisionAsync(first, TestContext.Current.CancellationToken);
            await provisioner.DeprovisionAsync(second, TestContext.Current.CancellationToken);
        }
    }

    [Fact]
    public async Task DatabasePerChild_RemovalRetainsTheDatabaseUnderANewNameAndDropsTheRole()
    {
        // Arrange
        var provisioner = new PostgreSqlChildDatabaseProvisioner(new TestClock());
        var context = CreateContext(ChildDatabaseStrategy.DatabasePerChild);
        var result = await provisioner.ProvisionAsync(context, TestContext.Current.CancellationToken);
        context.ProvisionedResource = result.ProvisionedResource;

        // Act
        await provisioner.DeprovisionAsync(context, TestContext.Current.CancellationToken);

        // Assert
        var retainedName = ProvisioningNames.ForRetainedDatabase(result.ProvisionedResource, TestClock.Now);
        Assert.Equal(1L, await ScalarAsync(_adminConnectionString, $"SELECT count(*) FROM pg_database WHERE datname = '{retainedName}';"));
        Assert.Equal(0L, await ScalarAsync(_adminConnectionString, $"SELECT count(*) FROM pg_database WHERE datname = '{result.ProvisionedResource}';"));
        Assert.Equal(0L, await ScalarAsync(_adminConnectionString, $"SELECT count(*) FROM pg_roles WHERE rolname = '{result.ProvisionedResource}';"));

        await ExecuteAsync(_adminConnectionString, $"DROP DATABASE \"{retainedName}\";");
    }

    [Fact]
    public async Task SchemaPerChild_CreatesASchemaOnlyItsOwnRoleCanWriteTo()
    {
        // Arrange
        var provisioner = new PostgreSqlChildDatabaseProvisioner(new TestClock());
        var first = CreateContext(ChildDatabaseStrategy.SchemaPerChild);
        var second = CreateContext(ChildDatabaseStrategy.SchemaPerChild);

        // Act
        var firstResult = await provisioner.ProvisionAsync(first, TestContext.Current.CancellationToken);
        var secondResult = await provisioner.ProvisionAsync(second, TestContext.Current.CancellationToken);

        try
        {
            // Assert: the child names the database of its schema, even when the pool's connection string does not.
            Assert.False(string.IsNullOrEmpty(new NpgsqlConnectionStringBuilder(firstResult.ConnectionString).Database));
            await ExecuteAsync(firstResult.ConnectionString, $"CREATE TABLE \"{firstResult.Schema}\".notes (id int);");
            var exception = await Record.ExceptionAsync(() => ExecuteAsync(secondResult.ConnectionString, $"CREATE TABLE \"{firstResult.Schema}\".stolen (id int);"));
            Assert.IsType<PostgresException>(exception);
        }
        finally
        {
            first.ProvisionedResource = firstResult.ProvisionedResource;
            second.ProvisionedResource = secondResult.ProvisionedResource;
            await provisioner.DeprovisionAsync(first, TestContext.Current.CancellationToken);
            await provisioner.DeprovisionAsync(second, TestContext.Current.CancellationToken);
        }

        Assert.Equal(0L, await ScalarAsync(_adminConnectionString, $"SELECT count(*) FROM pg_namespace WHERE nspname = '{firstResult.Schema}';"));
    }

    [Fact]
    public async Task ParentWithDatabasePerChild_SetsUpAChildInItsOwnPostgreSqlDatabase()
    {
        // Arrange
        RequireServer();

        await using var host = await TenantHierarchyTestHost.StartAsync(new Dictionary<string, string>
        {
            ["TenantHierarchy:DatabasePools:pg:DatabaseProvider"] = "Postgres",
            ["TenantHierarchy:DatabasePools:pg:ConnectionString"] = _adminConnectionString,
            ["TenantHierarchy:DatabasePools:pg:RetainRemovedDatabases"] = "false",
        });

        var password = $"Th-{Guid.NewGuid():N}!aA1";
        await host.SetupTenantAsync(ShellSettings.DefaultShellName, "Blank", "platform", password);
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var features = services.GetRequiredService<IShellFeaturesManager>();
            var platform = (await features.GetAvailableFeaturesAsync()).Single(feature => feature.Id == TenantHierarchyConstants.Features.Platform);
            await features.EnableFeaturesAsync([platform], force: true);
        });

        await host.CreateTenantAsync("pgfirm", "pgfirm.localhost");
        await host.SetupTenantAsync("pgfirm", "Blank", "owner", password);
        await host.InTenantAsync(ShellSettings.DefaultShellName, async services =>
        {
            var (result, errors) = await services.GetRequiredService<TenantHierarchyPlatformService>().MakeParentAsync("pgfirm", "pgfirm", "PG Firm", new ParentTenantPolicy
            {
                DatabaseStrategy = ChildDatabaseStrategy.DatabasePerChild,
                DatabasePool = "pg",
            });

            Assert.True(result.Succeeded, $"{result.Error} {string.Join(' ', errors.Values)}");
        });

        // Act
        var entry = await host.InTenantAsync("pgfirm", async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            var (result, created) = await manager.CreateAsync(new CreateChildTenantRequest
            {
                DisplayName = "PG Business",
                Slug = "pg-business",
                RecipeName = "Blank",
            });

            Assert.True(result.Succeeded, result.Error);

            return created;
        });

        await host.InTenantAsync("pgfirm", async services =>
        {
            var result = await services.GetRequiredService<ChildTenantManager>().SetupAsync(entry.EntryId);
            Assert.True(result.Succeeded, result.Error);
        });

        // Assert
        var child = host.GetSettings(entry.TenantName);
        Assert.True(child.IsRunning());
        Assert.Equal("Postgres", child["DatabaseProvider"]);
        Assert.Equal(ProvisioningNames.ForTenant(entry.TenantName), new NpgsqlConnectionStringBuilder(child["ConnectionString"]).Database);
        Assert.True(await ScalarAsync(child["ConnectionString"], "SELECT count(*) FROM information_schema.tables WHERE table_name = 'Document';") >= 1);

        // Removing the child drops its database.
        await host.InTenantAsync("pgfirm", async services =>
        {
            var manager = services.GetRequiredService<ChildTenantManager>();
            Assert.True((await manager.SuspendAsync(entry.EntryId)).Succeeded);
            var removal = await manager.RemoveAsync(entry.EntryId);
            Assert.True(removal.Succeeded, removal.Error);
        });

        Assert.Equal(0L, await ScalarAsync(_adminConnectionString, $"SELECT count(*) FROM pg_database WHERE datname = '{ProvisioningNames.ForTenant(entry.TenantName)}';"));
    }

    private static ChildDatabaseProvisioningContext CreateContext(ChildDatabaseStrategy strategy)
    {
        RequireServer();

        return new ChildDatabaseProvisioningContext
        {
            TenantName = TenantHierarchyNaming.GenerateTenantName(),
            Strategy = strategy,
            PoolName = "pg",
            Pool = new DatabasePoolOptions
            {
                DatabaseProvider = DatabaseProviderNames.PostgreSql,
                ConnectionString = _adminConnectionString,
            },
        };
    }

    private static void RequireServer()
    {
        if (string.IsNullOrEmpty(_adminConnectionString))
        {
            Assert.Skip($"Set {ConnectionStringVariable} to run the PostgreSQL provisioning tests.");
        }
    }

    private static async Task ExecuteAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarAsync(string connectionString, string sql)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand(sql, connection);

        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private sealed class TestClock : IClock
    {
        public static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => Now;

        public ITimeZone[] GetTimeZones() => throw new NotSupportedException();

        public ITimeZone GetTimeZone(string timeZoneId) => throw new NotSupportedException();

        public ITimeZone GetSystemTimeZone() => throw new NotSupportedException();

        public DateTimeOffset ConvertToTimeZone(DateTimeOffset dateTimeOffset, ITimeZone timeZone) => throw new NotSupportedException();
    }
}
