using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using YesSql;
using YesSql.Indexes;
using YesSql.Provider.Sqlite;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.DistributedTests.Infrastructure;

internal sealed class ContactCenterStoreTestHarness : IAsyncDisposable
{
    private ContactCenterStoreTestHarness(
        IStore store,
        string databasePath)
    {
        Store = store;
        DatabasePath = databasePath;
    }

    public IStore Store { get; }

    private string DatabasePath { get; }

    public static async Task<ContactCenterStoreTestHarness> CreateAsync(
        string databaseName,
        IEnumerable<IIndexProvider> indexProviders,
        Func<SchemaBuilder, Task> createSchemaAsync)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "StateAuthorityData");
        Directory.CreateDirectory(directory);

        var databasePath = Path.Combine(directory, $"{databaseName}-{Guid.NewGuid():N}.db");
        var store = StoreFactory.Create(configuration => configuration.UseSqLite($"Data Source={databasePath};Pooling=False"));
        store.RegisterIndexes(indexProviders);
        await store.InitializeAsync(TestContext.Current.CancellationToken);
        await store.InitializeCollectionAsync(ContactCenterConstants.CollectionName, TestContext.Current.CancellationToken);

        await using var session = store.CreateSession();
        var transaction = await session.BeginTransactionAsync(TestContext.Current.CancellationToken);
        var schemaBuilder = new SchemaBuilder(store.Configuration, transaction);
        await createSchemaAsync(schemaBuilder);
        await transaction.CommitAsync(TestContext.Current.CancellationToken);

        return new ContactCenterStoreTestHarness(store, databasePath);
    }

    public async ValueTask DisposeAsync()
    {
        Store.Dispose();

        if (File.Exists(DatabasePath))
        {
            File.Delete(DatabasePath);
        }
    }

    public static async Task CreateInteractionSchemaAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Channel", column => column.WithLength(50))
            .Column<string>("Direction", column => column.WithLength(50))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc")
            .Column<DateTime>("WrapUpStartedUtc")
            .Column<DateTime>("WrapUpCompletedUtc"),
            collection: ContactCenterConstants.CollectionName);
    }

    public static async Task CreateCallSessionSchemaAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<CallSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderCallId", column => column.WithLength(128))
            .Column<string>("ProviderCallClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(261))
            .Column<string>("State", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("AgentSessionId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("BridgeId", column => column.WithLength(128))
            .Column<string>("ConferenceId", column => column.WithLength(128))
            .Column<string>("RecordingId", column => column.WithLength(128))
            .Column<string>("SupervisorAgentId", column => column.WithLength(26))
            .Column<string>("SupervisorLegId", column => column.WithLength(128))
            .Column<string>("DurableCommandId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterConstants.CollectionName);
    }

    public static async Task CreateAgentSessionSchemaAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<AgentSessionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26))
            .Column<bool>("IsOnline")
            .Column<DateTime>("LastHeartbeatUtc"),
            collection: ContactCenterConstants.CollectionName);

        await CreateUniqueIndexAsync(schemaBuilder, typeof(AgentSessionIndex), "UQ_Test_AgentSessionIndex_UserId", "UserId");
    }

    public static async Task CreateQueueItemSchemaAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<QueueItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ActivityClaimKey", column => column.NotNull().WithDefault(string.Empty).WithLength(26))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<string>("Priority", column => column.WithLength(50))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<DateTime>("EnqueuedUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await CreateUniqueIndexAsync(schemaBuilder, typeof(QueueItemIndex), "UQ_Test_QueueItemIndex_ActivityClaimKey", "ActivityClaimKey");
    }

    public static async Task CreateInboxSchemaAsync(SchemaBuilder schemaBuilder)
    {
        await schemaBuilder.CreateMapIndexTableAsync<ProviderWebhookInboxMessageIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(100))
            .Column<string>("DeliveryId", column => column.WithLength(256))
            .Column<string>("Status", column => column.WithLength(50))
            .Column<DateTime>("NextAttemptUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName);

        await CreateUniqueIndexAsync(
            schemaBuilder,
            typeof(ProviderWebhookInboxMessageIndex),
            "UQ_Test_ProviderWebhookInboxMessageIndex_Delivery",
            "ProviderName",
            "DeliveryId");
    }

    private static async Task CreateUniqueIndexAsync(
        SchemaBuilder schemaBuilder,
        Type indexType,
        string indexName,
        params string[] columnNames)
    {
        var tableName = schemaBuilder.TablePrefix +
            schemaBuilder.TableNameConvention.GetIndexTable(indexType, ContactCenterConstants.CollectionName);
        var quotedTableName = schemaBuilder.Dialect.QuoteForTableName(tableName, null);
        var quotedIndexName = schemaBuilder.Dialect.QuoteForColumnName(schemaBuilder.Dialect.FormatIndexName(indexName));
        var quotedColumns = string.Join(", ", columnNames.Select(schemaBuilder.Dialect.QuoteForColumnName));

        await using var command = schemaBuilder.Connection.CreateCommand();
        command.Transaction = schemaBuilder.Transaction;
        command.CommandText = $"CREATE UNIQUE INDEX {quotedIndexName} ON {quotedTableName} ({quotedColumns})";
        await command.ExecuteNonQueryAsync();
    }
}
