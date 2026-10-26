using CrestApps.OrchardCore.Omnichannel.Messaging.Core;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Indexes;
using CrestApps.OrchardCore.Omnichannel.Messaging.Migrations;
using CrestApps.OrchardCore.Tests.Utilities;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// Covers the indexes the inbox counts and the Messaging admin menu badge are answered from.
/// </summary>
/// <remarks>
/// Every index the conversation table had led with <c>DocumentId</c>, so the badge's two counts after every admin page
/// and the inbox's three counts on every refresh each scanned the table, which saturated data IO on a small database
/// tier. A fresh install and an upgraded one must end with the same indexes, in the same column order.
/// </remarks>
public sealed class MessagingAttentionIndexMigrationTests
{
    private const string TablePrefix = "tenant_";

    private static readonly string[] _agentAttentionColumns = ["AssignedAgentId", "Status", "IsRead", "LastMessageUtc", "DocumentId"];
    private static readonly string[] _ownerAttentionColumns = ["OwnerType", "OwnerId", "Status", "IsRead", "AssignmentStatus", "AssignedAgentId", "DocumentId"];
    private static readonly string[] _activityColumns = ["LastActivityUtc", "DocumentId"];

    [Fact]
    public async Task CreateAsync_CreatesTheAttentionIndexes()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var version = await CreateMigration(store, schemaBuilder).CreateAsync();

            Assert.Equal(6, version);
            Assert.Equal(_agentAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.AgentAttentionIndexName));
            Assert.Equal(_ownerAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.OwnerAttentionIndexName));
            Assert.Equal(_activityColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.ActivityIndexName));
        });
    }

    // Live: a conversation transferred to an agent sat below every thread with a newer message, past the first page of
    // their inbox, because the inbox was ordered by the last message alone.
    [Fact]
    public async Task UpdateFrom4Async_OnAVersion4Table_AddsTheActivityColumnFromTheLastMessage()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            // What version 4 released: the same table without the activity column and its index.
            await DropIndexAsync(schemaBuilder, MessagingMigrations.ActivityIndexName);
            await ExecuteAsync(schemaBuilder, $"ALTER TABLE {TableName(schemaBuilder)} DROP COLUMN LastActivityUtc");
            await ExecuteAsync(schemaBuilder, $"INSERT INTO {TableName(schemaBuilder)} (DocumentId, ItemId, LastMessageUtc, IsRead, UnreadCount) VALUES (1, 'c1', '2026-10-24 05:59:00', 0, 0)");

            var version = await migration.UpdateFrom4Async();

            Assert.Equal(5, version);
            Assert.Equal(_activityColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.ActivityIndexName));
            Assert.Equal("2026-10-24 05:59:00", await ScalarAsync(schemaBuilder, $"SELECT LastActivityUtc FROM {TableName(schemaBuilder)} WHERE ItemId = 'c1'"));
        });
    }

    [Fact]
    public async Task UpdateFrom3Async_OnAVersion3Table_AddsTheAttentionIndexes()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            // What version 3 released: the same table without the two indexes.
            await DropIndexAsync(schemaBuilder, MessagingMigrations.AgentAttentionIndexName);
            await DropIndexAsync(schemaBuilder, MessagingMigrations.OwnerAttentionIndexName);
            Assert.Empty(await IndexColumnsAsync(schemaBuilder, MessagingMigrations.AgentAttentionIndexName));

            var version = await migration.UpdateFrom3Async();

            Assert.Equal(4, version);
            Assert.Equal(_agentAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.AgentAttentionIndexName));
            Assert.Equal(_ownerAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.OwnerAttentionIndexName));
        });
    }

    // A second app instance activating the tenant at the same time may have added them already.
    [Fact]
    public async Task UpdateFrom3Async_WhenTheIndexesAlreadyExist_RecordsTheVersionInsteadOfFailing()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            var version = await migration.UpdateFrom3Async();

            Assert.Equal(4, version);
        });
    }

    private static MessagingMigrations CreateMigration(IStore store, SchemaBuilder schemaBuilder)
        => new(store, NullLogger<MessagingMigrations>.Instance)
        {
            SchemaBuilder = schemaBuilder,
        };

    private static Task DropIndexAsync(SchemaBuilder schemaBuilder, string indexName)
        => schemaBuilder.AlterIndexTableAsync<MessagingConversationIndex>(
            table => table.DropIndex(indexName),
            collection: MessagingStorage.CollectionName);

    private static string TableName(SchemaBuilder schemaBuilder)
        => schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(MessagingConversationIndex), MessagingStorage.CollectionName);

    private static async Task ExecuteAsync(SchemaBuilder schemaBuilder, string sql)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<string> ScalarAsync(SchemaBuilder schemaBuilder, string sql)
    {
        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = sql;

        return Convert.ToString(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken), System.Globalization.CultureInfo.InvariantCulture);
    }

    // The indexed columns in order, or none when the index is missing.
    private static async Task<string[]> IndexColumnsAsync(SchemaBuilder schemaBuilder, string indexName)
    {
        if (!await IndexTableIndexCreation.ExistsAsync(schemaBuilder, typeof(MessagingConversationIndex), indexName, MessagingStorage.CollectionName))
        {
            return [];
        }

        var effectiveName = schemaBuilder.Dialect.FormatIndexName(
            schemaBuilder.Dialect.PrefixIndex ? schemaBuilder.TablePrefix + indexName : indexName);

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"SELECT name FROM pragma_index_info('{effectiveName}') ORDER BY seqno";

        var columns = new List<string>();

        await using var reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);

        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            columns.Add(reader.GetString(0));
        }

        return [.. columns];
    }

    private static async Task WithSchemaAsync(Func<IStore, SchemaBuilder, Task> test)
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"messaging-attention-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration
            .UseSqLite($"Data Source={databasePath};Pooling=False")
            .SetTablePrefix(TablePrefix));
        await store.InitializeAsync(TestContext.Current.CancellationToken);

        try
        {
            await using var session = store.CreateSession();
            var schemaBuilder = new SchemaBuilder(store.Configuration, await session.BeginTransactionAsync(TestContext.Current.CancellationToken));

            await test(store, schemaBuilder);
        }
        finally
        {
            TemporarySqliteDatabase.DisposeAndDelete(store, databasePath);
        }
    }
}
