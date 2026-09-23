using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Managements.Migrations;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using OrchardCore.Data;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel.Managements.Migrations;

/// <summary>
/// Runs the activity batch index upgrade chain inside the transaction the host runs it in.
/// </summary>
/// <remarks>
/// The column steps isolate each change and the repair step probes for the columns before adding them. Both must
/// do that work inside the host's transaction: on SQLite a second connection waits for the write lock that
/// transaction holds, and a probe on a second connection could not see the columns the earlier steps have just
/// added, because those are not committed until every step has run.
/// </remarks>
public sealed class OmnichannelActivityBatchIndexMigrationsTests
{
    private const int FinalVersion = 4;

    private static readonly TimeSpan _runLimit = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    public async Task UpgradeFromAReleasedSchema_InsideTheHostTransaction_AddsTheMissingColumnsAndCommits(int startingVersion)
    {
        var databasePath = DatabasePath("activity-batch-upgrade");
        var store = await CreateStoreAsync(databasePath);
        var connections = new CountingConnectionAccessor(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateVersion1TableAsync(schemaBuilder);
            var migration = new OmnichannelActivityBatchIndexMigrations(
                store,
                connections,
                NullLogger<OmnichannelActivityBatchIndexMigrations>.Instance);
            migration.SchemaBuilder = schemaBuilder;

            var version = await RunWithinLimitAsync(() => MigrationChainRunner.RunUpgradeChainAsync(migration, startingVersion));

            Assert.Equal(FinalVersion, version);
            Assert.Equal(0, connections.Created);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            var schema = await CaptureCommittedSchemaAsync(databasePath, TableName(store));
            Assert.Contains("Source type=", schema);
            Assert.Contains("CreatedUtc type=", schema);
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

    // The shape version 1 released: no source or creation time columns.
    private static async Task CreateVersion1TableAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("DisplayText", column => column.WithLength(255))
            .Column<string>("Status", column => column.WithLength(20)),
            collection: OmnichannelConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OmnichannelActivityBatchIndex>(table => table
            .CreateIndex("IDX_OmnichannelActivityBatchIndex_DocumentId", "DocumentId", "DisplayText", "ItemId"),
            collection: OmnichannelConstants.CollectionName);
    }

    private static async Task<string> CaptureCommittedSchemaAsync(string databasePath, string tableName)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        return await SqliteSchemaSnapshot.CaptureAsync(connection, null, tableName);
    }

    private static string TableName(IStore store)
        => store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelActivityBatchIndex), OmnichannelConstants.CollectionName);

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
