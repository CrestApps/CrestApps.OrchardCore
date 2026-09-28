using System.Data.Common;
using CrestApps.OrchardCore.Omnichannel.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Migrations;
using CrestApps.OrchardCore.Tests.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Modules.Omnichannel;

/// <summary>
/// Runs the contact preference index upgrade inside the transaction the host runs it in.
/// </summary>
/// <remarks>
/// The host applies every step of a feature's migrations on one session transaction, and on SQLite that
/// transaction holds the database's only write lock for as long as the steps run. A step that does its work on
/// a second connection therefore waits for a lock its own caller will never release: every startup stalls for
/// the full busy timeout and then fails, and the step is retried, and fails again, on the next one. Calling the
/// step outside that transaction hides the defect entirely, so each case here holds the transaction open with a
/// write already made in it, the way the host does.
/// </remarks>
public sealed class OmnichannelContactCommunicationPreferenceIndexMigrationsTests
{
    [Fact]
    public async Task UpgradeFromVersion2_InsideTheHostTransaction_DropsTheChatColumnsAndKeepsEveryPreference()
    {
        var databasePath = DatabasePath("contact-preference-upgrade");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateReleasedTableAsync(schemaBuilder);
            await InsertPreferenceAsync(schemaBuilder, store, documentId: 1, contentItemId: "contact-1", doNotCall: true);
            var migration = CreateMigration(store);
            migration.SchemaBuilder = schemaBuilder;

            var version = await MigrationChainRunner.RunUpgradeChainAsync(migration, 2);

            Assert.Equal(3, version);
            var columns = await ReadColumnNamesAsync(schemaBuilder, store);
            Assert.DoesNotContain("DoNotChat", columns);
            Assert.DoesNotContain("DoNotChatUtc", columns);
            Assert.True(await ReadDoNotCallAsync(schemaBuilder, store, "contact-1"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Fact]
    public async Task UpgradeFromVersion2_WhenTheTableNeverHadTheChatColumns_CompletesAndLeavesTheTransactionUsable()
    {
        var databasePath = DatabasePath("contact-preference-no-chat");
        var store = await CreateStoreAsync(databasePath);

        try
        {
            await using var session = store.CreateSession();
            var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
            var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
            await CreateCurrentTableAsync(schemaBuilder);
            await InsertPreferenceAsync(schemaBuilder, store, documentId: 1, contentItemId: "contact-1", doNotCall: true);
            var migration = CreateMigration(store);
            migration.SchemaBuilder = schemaBuilder;

            var version = await MigrationChainRunner.RunUpgradeChainAsync(migration, 2);

            // A sibling step's work in the same transaction must survive, and the transaction must still commit,
            // because a failed drop on the shared transaction would take every sibling step down with it.
            Assert.Equal(3, version);
            await InsertPreferenceAsync(schemaBuilder, store, documentId: 2, contentItemId: "contact-2", doNotCall: false);
            await transaction.CommitAsync(TestContext.Current.CancellationToken);
            Assert.True(await ReadCommittedDoNotCallAsync(databasePath, store, "contact-1"));
            Assert.False(await ReadCommittedDoNotCallAsync(databasePath, store, "contact-2"));
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public async Task PreferenceIndex_FreshTenantAndTenantUpgradedFromAnyReleasedSchema_HaveTheSameShape(int startingVersion)
    {
        var freshPath = DatabasePath("contact-preference-fresh");
        var upgradedPath = DatabasePath("contact-preference-upgraded");
        var freshStore = await CreateStoreAsync(freshPath);
        var upgradedStore = await CreateStoreAsync(upgradedPath);

        try
        {
            string freshSchema;
            string upgradedSchema;
            int freshVersion;
            int upgradedVersion;

            await using (var session = freshStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(freshStore.Configuration, transaction);
                var migration = CreateMigration(freshStore);
                migration.SchemaBuilder = schemaBuilder;

                freshVersion = await migration.CreateAsync();
                freshSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(freshStore));
            }

            await using (var session = upgradedStore.CreateSession())
            {
                var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
                var schemaBuilder = new SchemaBuilder(upgradedStore.Configuration, transaction);
                await CreateReleasedTableAsync(schemaBuilder);
                var migration = CreateMigration(upgradedStore);
                migration.SchemaBuilder = schemaBuilder;

                upgradedVersion = await MigrationChainRunner.RunUpgradeChainAsync(migration, startingVersion);
                upgradedSchema = await SqliteSchemaSnapshot.CaptureAsync(schemaBuilder.Connection, transaction, TableName(upgradedStore));
            }

            Assert.Equal(freshVersion, upgradedVersion);
            Assert.Equal(freshSchema, upgradedSchema);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(freshStore, freshPath);
            TemporarySqliteDatabase.DisposeAndDelete(upgradedStore, upgradedPath);
        }
    }

    private static OmnichannelContactCommunicationPreferenceIndexMigrations CreateMigration(IStore store)
        => new(store, NullLogger<OmnichannelContactCommunicationPreferenceIndexMigrations>.Instance);

    // The shape every released version created: versions 1 and 2 both built the same table, chat preference
    // columns included, with the same opt-out index.
    private static async Task CreateReleasedTableAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotCallUtc")
            .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotSmsUtc")
            .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotEmailUtc")
            .Column<bool>("DoNotChat", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotChatUtc"));

        await schemaBuilder.AlterIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
            .CreateIndex("IDX_OmnichannelContactCommunicationPreferenceIndex_DoNotCallUtc", "DocumentId", "DoNotCallUtc"));
    }

    private static Task CreateCurrentTableAsync(SchemaBuilder schemaBuilder)
        => schemaBuilder.CreateMapIndexTableAsync<OmnichannelContactCommunicationPreferenceIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<bool>("DoNotCall", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotCallUtc")
            .Column<bool>("DoNotSms", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotSmsUtc")
            .Column<bool>("DoNotEmail", column => column.NotNull().WithDefault(false))
            .Column<DateTime>("DoNotEmailUtc"));

    private static async Task InsertPreferenceAsync(SchemaBuilder schemaBuilder, IStore store, int documentId, string contentItemId, bool doNotCall)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"INSERT INTO {QuotedTableName(store)} (DocumentId, ContentItemId, DoNotCall) VALUES ({documentId}, @id, @doNotCall)";
        AddParameter(command, "@id", contentItemId);
        AddParameter(command, "@doNotCall", doNotCall);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<bool> ReadDoNotCallAsync(SchemaBuilder schemaBuilder, IStore store, string contentItemId)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;

        return await ReadDoNotCallAsync(command, store, contentItemId);
    }

    private static async Task<bool> ReadCommittedDoNotCallAsync(string databasePath, IStore store, string contentItemId)
    {
        await using var connection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();

        return await ReadDoNotCallAsync(command, store, contentItemId);
    }

    private static async Task<bool> ReadDoNotCallAsync(DbCommand command, IStore store, string contentItemId)
    {
        command.CommandText = $"SELECT DoNotCall FROM {QuotedTableName(store)} WHERE ContentItemId = @id";
        AddParameter(command, "@id", contentItemId);

        return Convert.ToBoolean(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<HashSet<string>> ReadColumnNamesAsync(SchemaBuilder schemaBuilder, IStore store)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT * FROM {QuotedTableName(store)} WHERE 1 = 0";
        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        return Enumerable.Range(0, reader.FieldCount).Select(reader.GetName).ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static void AddParameter(DbCommand command, string name, object value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }

    private static string TableName(IStore store)
        => store.Configuration.TablePrefix +
            store.Configuration.TableNameConvention.GetIndexTable(typeof(OmnichannelContactCommunicationPreferenceIndex), null);

    private static string QuotedTableName(IStore store)
        => store.Configuration.SqlDialect.QuoteForTableName(TableName(store), store.Configuration.Schema);

    private static string DatabasePath(string name)
        => Path.Combine(Path.GetTempPath(), $"{name}-{Guid.NewGuid():N}.db");

    private static async Task<IStore> CreateStoreAsync(string databasePath)
    {
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        return store;
    }
}
