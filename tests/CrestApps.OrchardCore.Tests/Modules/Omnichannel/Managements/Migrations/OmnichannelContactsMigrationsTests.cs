using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.Data;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

/// <summary>
/// Runs the contact index upgrade chain inside the transaction the host runs it in.
/// </summary>
/// <remarks>
/// The host applies every step of a feature's migrations on one session transaction, and on SQLite that
/// transaction holds the database's only write lock as soon as any step writes. The contact index steps isolate
/// each idempotent change so a failure cannot poison that shared transaction, and an isolation that moves the
/// change onto a second connection waits for a lock its own caller never releases: it hits the busy timeout, the
/// failure is taken for "already exists", and the change is silently skipped. Calling the steps outside that
/// transaction hides the defect entirely, so each case here holds the transaction open with a write already made
/// in it, the way the host does, and bounds the run well inside the busy timeout.
/// </remarks>
public sealed class OmnichannelContactsMigrationsTests
{
    private const int FinalVersion = 11;
    private const string LegacyPhoneIndexTableName = "OmnichannelContactPhoneIndex";

    private static readonly TimeSpan _runLimit = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task UpgradeFromVersion1_InsideTheHostTransaction_AppliesEveryChangeAndCommits()
    {
        var databasePath = DatabasePath("contacts-upgrade");
        var store = await CreateStoreAsync(databasePath);
        var connections = new CountingConnectionAccessor(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateVersion1TableAsync(schemaBuilder);
            await InsertContactAsync(schemaBuilder, store, documentId: 1, contentItemId: "contact-1");
            var migration = CreateMigration(store, connections);
            migration.SchemaBuilder = schemaBuilder;

            var version = await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, 1));

            Assert.Equal(FinalVersion, version);
            Assert.Equal(0, connections.Created);
            var schema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(store));
            Assert.Contains("TimeZoneId type=", schema);
            Assert.Contains("IDX_OmnichannelContactIndex_TimeZoneId ", schema);
            Assert.Contains("IDX_OCIndex_TimeZoneVersion ", schema);
            Assert.Contains("IDX_OCIndex_ContentItemLatest ", schema);

            await transaction.CommitAsync(TestContext.Current.CancellationToken);

            Assert.Equal(schema, await CaptureCommittedSchemaAsync(databasePath, TableName(store)));
            Assert.True(await CommittedContactExistsAsync(databasePath, store, "contact-1"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeFromVersion1_InsideTheHostTransaction_ProducesTheSameShapeAsAFreshTenant()
    {
        var freshPath = DatabasePath("contacts-fresh");
        var upgradedPath = DatabasePath("contacts-upgraded");
        var freshStore = await CreateStoreAsync(freshPath);
        var upgradedStore = await CreateStoreAsync(upgradedPath);

        try
        {
            string freshSchema;
            string upgradedSchema;

            await using (var session = freshStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(freshStore.Configuration, transaction);
                var migration = CreateMigration(freshStore, new CountingConnectionAccessor(freshPath));
                migration.SchemaBuilder = schemaBuilder;

                Assert.Equal(FinalVersion, await migration.CreateAsync());
                freshSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(freshStore));
            }

            await using (var session = upgradedStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(upgradedStore.Configuration, transaction);
                await CreateVersion1TableAsync(schemaBuilder);
                var migration = CreateMigration(upgradedStore, new CountingConnectionAccessor(upgradedPath));
                migration.SchemaBuilder = schemaBuilder;

                Assert.Equal(FinalVersion, await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, 1)));
                upgradedSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(upgradedStore));
            }

            Assert.Equal(freshSchema, upgradedSchema);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(freshStore, freshPath);
            TemporarySqliteDatabase.DisposeAndDelete(upgradedStore, upgradedPath);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task UpgradeOverACurrentSchema_SkipsEveryChangeThatAlreadyExistsWithoutPoisoningTheTransaction(int startingVersion)
    {
        var databasePath = DatabasePath("contacts-already-current");
        var store = await CreateStoreAsync(databasePath);
        var connections = new CountingConnectionAccessor(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            var migration = CreateMigration(store, connections);
            migration.SchemaBuilder = schemaBuilder;
            await migration.CreateAsync();
            await InsertContactAsync(schemaBuilder, store, documentId: 1, contentItemId: "contact-1");
            var schemaBefore = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(store));

            var version = await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, startingVersion));

            // Every change here fails because its object already exists, and none of those failures may undo the
            // work a sibling step already made in the same transaction or stop the transaction from committing.
            Assert.Equal(FinalVersion, version);
            Assert.Equal(0, connections.Created);
            Assert.Equal(schemaBefore, await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(store)));
            await InsertContactAsync(schemaBuilder, store, documentId: 2, contentItemId: "contact-2");
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            Assert.True(await CommittedContactExistsAsync(databasePath, store, "contact-1"));
            Assert.True(await CommittedContactExistsAsync(databasePath, store, "contact-2"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeFromVersion7_InsideTheHostTransaction_DropsTheEmptyLegacyTablesAndCommits()
    {
        var databasePath = DatabasePath("contacts-legacy-drop");
        var store = await CreateStoreAsync(databasePath);
        var connections = new CountingConnectionAccessor(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateVersion1TableAsync(schemaBuilder);
            await CreateLegacyCollectionTableAsync(schemaBuilder);
            await CreateLegacyPhoneTableAsync(schemaBuilder, store);
            var migration = CreateMigration(store, connections);
            migration.SchemaBuilder = schemaBuilder;

            var version = await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, 7));

            Assert.Equal(FinalVersion, version);
            Assert.Equal(0, connections.Created);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            var tables = await ReadCommittedTableNamesAsync(databasePath);
            Assert.DoesNotContain(LegacyCollectionTableName(store), tables);
            Assert.DoesNotContain(LegacyPhoneTableName(store), tables);
            Assert.Contains(TableName(store), tables);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeFromVersion7_WhenTheLegacyCollectionTableStillHoldsRows_KeepsItAndCommits()
    {
        var databasePath = DatabasePath("contacts-legacy-keep");
        var store = await CreateStoreAsync(databasePath);
        var connections = new CountingConnectionAccessor(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateVersion1TableAsync(schemaBuilder);
            await CreateLegacyCollectionTableAsync(schemaBuilder);
            await ExecuteAsync(schemaBuilder, $"INSERT INTO {Quote(store, LegacyCollectionTableName(store))} (DocumentId, ContentItemId) VALUES (1, 'legacy-contact')");
            var migration = CreateMigration(store, connections);
            migration.SchemaBuilder = schemaBuilder;

            var version = await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, 7));

            Assert.Equal(FinalVersion, version);
            Assert.Equal(0, connections.Created);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            var tables = await ReadCommittedTableNamesAsync(databasePath);
            Assert.Contains(LegacyCollectionTableName(store), tables);
            Assert.Contains(TableName(store), tables);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    // The chain is started on a worker thread because SQLite's asynchronous methods complete synchronously, so a
    // step stuck waiting for the write lock would otherwise block the caller before any time limit could apply.
    private static async Task<int> RunWithinLimitAsync(Func<Task<int>> run)
    {
        var task = Task.Run(run, TestContext.Current.CancellationToken);
        var completed = await Task.WhenAny(task, Task.Delay(_runLimit, TestContext.Current.CancellationToken));

        Assert.True(
            completed == task,
            $"The upgrade chain did not finish within {_runLimit.TotalSeconds} seconds, so a step is waiting for the write lock the host transaction holds.");

        return await task;
    }

    private static OmnichannelContactsMigrations CreateMigration(IStore store, IDbConnectionAccessor connections)
        => new(
            Mock.Of<IContentDefinitionManager>(),
            store,
            connections,
            NullLogger<OmnichannelContactsMigrations>.Instance);

    // The shape version 1 released: no normalized phone, time zone, or version columns, and only the document index.
    private static async Task CreateVersion1TableAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("PrimaryCellPhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryHomePhoneNumber", column => column.WithLength(50))
            .Column<string>("PrimaryEmailAddress", column => column.WithLength(255)));

        await schemaBuilder.AlterIndexTableAsync<OmnichannelContactIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactIndex_DocumentId", "DocumentId", "ContentItemId"));
    }

    private static Task CreateLegacyCollectionTableAsync(SchemaBuilder schemaBuilder)
        => schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26)),
            collection: OmnichannelConstants.CollectionName);

    private static Task CreateLegacyPhoneTableAsync(SchemaBuilder schemaBuilder, IStore store)
        => ExecuteAsync(schemaBuilder, $"CREATE TABLE {Quote(store, LegacyPhoneTableName(store))} (Id INTEGER PRIMARY KEY, DocumentId INTEGER)");

    private static Task InsertContactAsync(SchemaBuilder schemaBuilder, IStore store, int documentId, string contentItemId)
        => ExecuteAsync(schemaBuilder, $"INSERT INTO {Quote(store, TableName(store))} (DocumentId, ContentItemId) VALUES ({documentId}, '{contentItemId}')");

    private static async Task ExecuteAsync(SchemaBuilder schemaBuilder, string sql)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<bool> CommittedContactExistsAsync(string databasePath, IStore store, string contentItemId)
    {
        await using var connection = await OpenCommittedAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {Quote(store, TableName(store))} WHERE ContentItemId = '{contentItemId}'";

        return Convert.ToInt32(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken)) == 1;
    }

    private static async Task<HashSet<string>> ReadCommittedTableNamesAsync(string databasePath)
    {
        await using var connection = await OpenCommittedAsync(databasePath);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table'";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static async Task<string> CaptureCommittedSchemaAsync(string databasePath, string tableName)
    {
        await using var connection = await OpenCommittedAsync(databasePath);

        return await SqliteSchemaSnapshot.CaptureAsync(connection, null, tableName);
    }

    private static async Task<SqliteConnection> OpenCommittedAsync(string databasePath)
    {
        var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return connection;
    }

    private static string TableName(IStore store)
        => store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex), null);

    private static string LegacyCollectionTableName(IStore store)
        => store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactIndex), OmnichannelConstants.CollectionName);

    private static string LegacyPhoneTableName(IStore store)
        => store.Configuration.TablePrefix + LegacyPhoneIndexTableName;

    private static string Quote(IStore store, string tableName)
        => store.Configuration.SqlDialect.QuoteForTableName(tableName, store.Configuration.Schema);

    private static string DatabasePath(string name)
        => Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        return store;
    }

    // Hands out real connections to the same database, as the host's accessor does, and counts them so a test can
    // show that no step reached for a second connection while the host transaction was open.
    private sealed class CountingConnectionAccessor : IDbConnectionAccessor
    {
        private readonly string _connectionString;
        private int _created;

        public CountingConnectionAccessor(string databasePath)
        {
            _connectionString = $"Data Source={databasePath};Pooling=False";
        }

        public int Created => Volatile.Read(ref _created);

        public DbConnection CreateConnection()
        {
            Interlocked.Increment(ref _created);

            return new SqliteConnection(_connectionString);
        }
    }
}
