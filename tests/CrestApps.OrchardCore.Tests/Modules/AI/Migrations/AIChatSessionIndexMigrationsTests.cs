using CrestApps.Core.AI.Models;
using CrestApps.Core.Data.YesSql;
using CrestApps.Core.Data.YesSql.Indexes.AIChat;
using CrestApps.OrchardCore.AI.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Services;
using YesSql.Sql;
using YesSql.Sql.Schema;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Migrations;

/// <summary>
/// A site whose chat session index is at version 5, from before CrestApps.Core preview.375 added <c>ClientId</c>,
/// can save chat sessions again after the upgrade, on SQLite with a tenant's table prefix.
/// </summary>
public sealed class AIChatSessionIndexMigrationsTests
{
    private static readonly YesSqlStoreOptions _storeOptions = new();

    [Fact]
    public async Task Version5Site_SavesChatSessionsAfterUpgrade()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await CreateVersion5TableAsync(store);

            // The scenario: before the upgrade step runs, the index insert fails.
            await using (var before = store.CreateSession())
            {
                await before.SaveAsync(NewSession("before", clientId: "visitor-0"), collection: _storeOptions.AICollectionName, cancellationToken: TestContext.Current.CancellationToken);

                var failure = await Assert.ThrowsAnyAsync<Exception>(() => before.SaveChangesAsync(TestContext.Current.CancellationToken));
                Assert.Contains("ClientId", failure.ToString(), StringComparison.OrdinalIgnoreCase);
            }

            int version;

            await using (var migrationSession = store.CreateSession())
            {
                var transaction = await migrationSession.BeginTransactionAsync(TestContext.Current.CancellationToken);
                version = await CreateMigrations(new SchemaBuilder(store.Configuration, transaction), store).UpdateFrom5Async();
                await transaction.CommitAsync(TestContext.Current.CancellationToken);
            }

            await using (var save = store.CreateSession())
            {
                await save.SaveAsync(NewSession("after", clientId: "visitor-1"), collection: _storeOptions.AICollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await save.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using var read = store.CreateSession();
            var found = await read
                .Query<AIChatSession, AIChatSessionIndex>(index => index.ClientId == "visitor-1", _storeOptions.AICollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal(6, version);
            Assert.Equal("after", Assert.Single(found).SessionId);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeStep_IsSafeWhenTheColumnAlreadyExists()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            Assert.Equal(6, await CreateMigrations(schemaBuilder, store).CreateAsync());
            Assert.Equal(6, await CreateMigrations(schemaBuilder, store).UpdateFrom5Async());
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task Backfill_FillsTheClientIdOfStoredAnonymousSessions()
    {
        var databasePath = DatabasePath();
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using (var setup = store.CreateSession())
            {
                var schemaBuilder = new SchemaBuilder(store.Configuration, await setup.BeginTransactionAsync(TestContext.Current.CancellationToken));
                await CreateMigrations(schemaBuilder, store).CreateAsync();
                await setup.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            await using (var save = store.CreateSession())
            {
                await save.SaveAsync(NewSession("anonymous", clientId: "visitor-2"), collection: _storeOptions.AICollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await save.SaveAsync(NewSession("signed-in", clientId: null, userId: "user-1"), collection: _storeOptions.AICollectionName, cancellationToken: TestContext.Current.CancellationToken);
                await save.SaveChangesAsync(TestContext.Current.CancellationToken);
            }

            // An index row written before preview.375 has no client id.
            await ClearIndexedClientIdsAsync(store, databasePath);

            await using (var backfill = store.CreateSession())
            {
                await AIChatSessionIndexMigrations.BackfillClientIdsAsync(backfill, _storeOptions, NullLogger.Instance);
            }

            await using var read = store.CreateSession();
            var found = await read
                .Query<AIChatSession, AIChatSessionIndex>(index => index.ClientId == "visitor-2", _storeOptions.AICollectionName)
                .ListAsync(TestContext.Current.CancellationToken);
            var all = await read
                .Query<AIChatSession, AIChatSessionIndex>(_storeOptions.AICollectionName)
                .ListAsync(TestContext.Current.CancellationToken);

            Assert.Equal("anonymous", Assert.Single(found).SessionId);
            Assert.Equal(2, all.Count);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeStep_LooksForTheColumnInTheStoresSchema()
    {
        // Review 7 (Mild): on PostgreSQL or SQL Server with a custom schema, the column check looked in the connection's
        // default schema, did not find the column a preview.375 build had created, and added it again on every
        // activation. A SQLite connection stands in for PostgreSQL here: it answers the catalog query the package sends,
        // with the column and its index recorded in the tenant's own schema only.
        const string tenantSchema = "tenant_schema";
        var table = "tenant_" + new DefaultTableNameConvention().GetIndexTable(typeof(AIChatSessionIndex), _storeOptions.AICollectionName);

        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        connection.CreateFunction("current_schema", () => "public");

        await using (var setup = connection.CreateCommand())
        {
            setup.CommandText = $"""
                ATTACH DATABASE ':memory:' AS information_schema;
                CREATE TABLE information_schema.columns (table_schema TEXT, table_name TEXT, column_name TEXT);
                CREATE TABLE pg_indexes (schemaname TEXT, tablename TEXT, indexname TEXT);
                INSERT INTO information_schema.columns VALUES ('{tenantSchema}', '{table}', 'ClientId');
                INSERT INTO pg_indexes VALUES ('{tenantSchema}', '{table}', 'IDX_AIChatSession_ClientId');
                """;
            await setup.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var dialect = new Mock<ISqlDialect>();
        dialect.SetupGet(d => d.Name).Returns("PostgreSql");
        var schemaBuilder = new Mock<ISchemaBuilder>();
        schemaBuilder.SetupGet(s => s.Dialect).Returns(dialect.Object);
        schemaBuilder.SetupGet(s => s.Connection).Returns(connection);
        schemaBuilder.SetupGet(s => s.TablePrefix).Returns("tenant_");
        schemaBuilder.SetupGet(s => s.TableNameConvention).Returns(new DefaultTableNameConvention());
        schemaBuilder
            .Setup(s => s.AlterIndexTableAsync(It.IsAny<Type>(), It.IsAny<Action<IAlterTableCommand>>(), It.IsAny<string>()))
            .Returns(Task.CompletedTask);
        var store = new Mock<IStore>();
        store.SetupGet(s => s.Configuration).Returns(new global::YesSql.Configuration { Schema = tenantSchema });

        Assert.Equal(6, await CreateMigrations(schemaBuilder.Object, store.Object).UpdateFrom5Async());

        schemaBuilder.Verify(s => s.AlterIndexTableAsync(It.IsAny<Type>(), It.IsAny<Action<IAlterTableCommand>>(), It.IsAny<string>()), Times.Never);
    }

    private static AIChatSessionIndexMigrations CreateMigrations(ISchemaBuilder schemaBuilder, IStore store)
        => new(Options.Create(_storeOptions), store, NullLogger<AIChatSessionIndexMigrations>.Instance)
        {
            SchemaBuilder = schemaBuilder,
        };

    private static AIChatSession NewSession(string sessionId, string clientId, string userId = null)
        => new()
        {
            SessionId = sessionId,
            ProfileId = "profile-1",
            UserId = userId,
            ClientId = clientId,
            Status = ChatSessionStatus.Active,
            LastActivityUtc = new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc),
        };

    /// <summary>
    /// Builds the index table exactly as version 5 of the migration left it: no <c>ClientId</c> column.
    /// </summary>
    private static async Task CreateVersion5TableAsync(IStore store)
    {
        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);

        await schemaBuilder.CreateMapIndexTableAsync<AIChatSessionIndex>(table => table
            .Column<string>("SessionId", column => column.WithLength(26))
            .Column<string>("ProfileId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(255))
            .Column<ChatSessionStatus>("Status")
            .Column<DateTime>("LastActivityUtc"),
            _storeOptions.AICollectionName);

        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task ClearIndexedClientIdsAsync(IStore store, string databasePath)
    {
        var table = store.Configuration.TablePrefix + store.Configuration.TableNameConvention.GetIndexTable(typeof(AIChatSessionIndex), _storeOptions.AICollectionName);

        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = $"UPDATE \"{table}\" SET \"ClientId\" = NULL";

        Assert.Equal(2, await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
    }

    private static string DatabasePath()
        => Path.Combine(Path.GetTempPath(), $"ai-chat-session-index-migrations-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix("tenant_"));

        store.RegisterIndexes([new AIChatSessionIndexProvider(Options.Create(_storeOptions))], _storeOptions.AICollectionName);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(_storeOptions.AICollectionName, TestContext.Current.CancellationToken);

        return store;
    }
}
