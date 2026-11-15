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
/// Covers the indexes the inbox counts, the Messaging admin menu badge and the conversation lookups are answered from.
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
    private const string FirstResponseDueIndexName = "IDX_MessagingConversationIndex_FirstResponseDue";
    private const string ContactIndexName = "IDX_MessagingConversationIndex_Contact";
    private const string CampaignIndexName = "IDX_MessagingConversationIndex_Campaign";

    private static readonly string[] _activityColumns = ["LastActivityUtc", "DocumentId"];
    private static readonly string[] _firstResponseDueColumns = ["Status", "FirstResponseDueUtc", "DocumentId"];
    private static readonly string[] _contactColumns = ["Channel", "ContactAddress", "DocumentId"];
    private static readonly string[] _campaignColumns = ["CampaignId", "DocumentId"];

    [Fact]
    public async Task CreateAsync_CreatesTheAttentionIndexes()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var version = await CreateMigration(store, schemaBuilder).CreateAsync();

            Assert.Equal(8, version);
            Assert.Equal(_campaignColumns, await IndexColumnsAsync(schemaBuilder, CampaignIndexName));
            Assert.Equal("0", await ScalarAsync(schemaBuilder, $"SELECT COUNT(*) FROM {TemplateTableName(schemaBuilder)} WHERE Scope IS NOT NULL"));
            Assert.Equal(_agentAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.AgentAttentionIndexName));
            Assert.Equal(_ownerAttentionColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.OwnerAttentionIndexName));
            Assert.Equal(_activityColumns, await IndexColumnsAsync(schemaBuilder, MessagingMigrations.ActivityIndexName));
            Assert.Equal(_firstResponseDueColumns, await IndexColumnsAsync(schemaBuilder, FirstResponseDueIndexName));
            Assert.Equal(_contactColumns, await IndexColumnsAsync(schemaBuilder, ContactIndexName));
        });
    }

    // The inbox's campaign filter reads the campaign a conversation was handed off from.
    [Fact]
    public async Task UpdateFrom6Async_OnAVersion6Table_AddsTheCampaignColumnAndIndex()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            // What version 6 released: the same table without the campaign column and its index.
            await DropIndexAsync(schemaBuilder, CampaignIndexName);
            await ExecuteAsync(schemaBuilder, $"ALTER TABLE {TableName(schemaBuilder)} DROP COLUMN CampaignId");
            await ExecuteAsync(schemaBuilder, $"INSERT INTO {TableName(schemaBuilder)} (DocumentId, ItemId, LastMessageUtc, IsRead, UnreadCount) VALUES (1, 'c1', '2026-10-24 05:59:00', 0, 0)");

            Assert.Equal(7, await migration.UpdateFrom6Async());
            Assert.Equal(_campaignColumns, await IndexColumnsAsync(schemaBuilder, CampaignIndexName));
            Assert.True(string.IsNullOrEmpty(await ScalarAsync(schemaBuilder, $"SELECT CampaignId FROM {TableName(schemaBuilder)} WHERE ItemId = 'c1'")));
        });
    }

    // A reply template can be written for another kind of work, such as tickets; existing templates stay the workspace's.
    // Version 7 is the campaign step that main released, so a tenant built from main gets the scope as version 8.
    [Fact]
    public async Task UpdateFrom7Async_OnTheVersion7MainReleased_AddsTheTemplateScope()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            // What version 7 released: the campaign column and index, and the template table without the scope column.
            await ExecuteAsync(schemaBuilder, $"ALTER TABLE {TemplateTableName(schemaBuilder)} DROP COLUMN Scope");
            await ExecuteAsync(schemaBuilder, $"INSERT INTO {TemplateTableName(schemaBuilder)} (DocumentId, ItemId, Name) VALUES (1, 't1', 'Thanks')");

            Assert.Equal(8, await migration.UpdateFrom7Async());
            Assert.Equal("1", await ScalarAsync(schemaBuilder, $"SELECT COUNT(*) FROM {TemplateTableName(schemaBuilder)} WHERE ItemId = 't1' AND Scope IS NULL"));
            Assert.Equal(_campaignColumns, await IndexColumnsAsync(schemaBuilder, CampaignIndexName));
        });
    }

    // A tenant at version 6 runs main's step and then the scope step, and ends where a fresh install does.
    [Fact]
    public async Task AVersion6Tenant_RunsBothSteps_AndEndsLikeAFreshInstall()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            await DropIndexAsync(schemaBuilder, CampaignIndexName);
            await ExecuteAsync(schemaBuilder, $"ALTER TABLE {TableName(schemaBuilder)} DROP COLUMN CampaignId");
            await ExecuteAsync(schemaBuilder, $"ALTER TABLE {TemplateTableName(schemaBuilder)} DROP COLUMN Scope");

            Assert.Equal(7, await migration.UpdateFrom6Async());
            Assert.Equal(8, await migration.UpdateFrom7Async());
            Assert.Equal(_campaignColumns, await IndexColumnsAsync(schemaBuilder, CampaignIndexName));
            Assert.Equal("0", await ScalarAsync(schemaBuilder, $"SELECT COUNT(*) FROM {TemplateTableName(schemaBuilder)} WHERE Scope IS NOT NULL"));
        });
    }

    // The first-response deadline check runs on a timer and every earlier index led with DocumentId, so each run read
    // the whole conversation table.
    [Fact]
    public async Task UpdateFrom5Async_OnAVersion5Table_AddsTheLookupIndexesOnce()
    {
        await WithSchemaAsync(async (store, schemaBuilder) =>
        {
            var migration = CreateMigration(store, schemaBuilder);
            await migration.CreateAsync();

            // What version 5 released: the same table without the two indexes.
            await DropIndexAsync(schemaBuilder, FirstResponseDueIndexName);
            await DropIndexAsync(schemaBuilder, ContactIndexName);

            Assert.Equal(6, await migration.UpdateFrom5Async());
            Assert.Equal(_firstResponseDueColumns, await IndexColumnsAsync(schemaBuilder, FirstResponseDueIndexName));
            Assert.Equal(_contactColumns, await IndexColumnsAsync(schemaBuilder, ContactIndexName));

            // A second instance activating the tenant at the same time finds them and records the version.
            Assert.Equal(6, await migration.UpdateFrom5Async());
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

    private static string TemplateTableName(SchemaBuilder schemaBuilder)
        => schemaBuilder.TablePrefix + schemaBuilder.TableNameConvention.GetIndexTable(typeof(MessageTemplateIndex), MessagingStorage.CollectionName);

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
