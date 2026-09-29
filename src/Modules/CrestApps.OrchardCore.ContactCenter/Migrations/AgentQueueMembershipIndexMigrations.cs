using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="AgentQueueMembershipIndex"/>.
/// </summary>
internal sealed class AgentQueueMembershipIndexMigrations : DataMigration
{
    private readonly IStore _store;

    /// <summary>
    /// Initializes a new instance of the <see cref="AgentQueueMembershipIndexMigrations"/> class.
    /// </summary>
    /// <param name="store">The YesSql store.</param>
    public AgentQueueMembershipIndexMigrations(IStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Creates the agent queue membership index table.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("QueueId", column => column.WithLength(ContactCenterStorage.QueueIdLength))
            .Column<AgentPresenceStatus>("PresenceStatus")
            .Column<int>("MaxConcurrentInteractions"),
            collection: ContactCenterStorage.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .CreateIndex(
                "IDX_AgentQueueMembershipIndex_Queue",
                "DocumentId",
                "QueueId",
                "PresenceStatus",
                "ItemId"),
            collection: ContactCenterStorage.CollectionName
        );

        return 1;
    }

    /// <summary>
    /// Widens the queue column so a campaign's virtual queue is stored rather than refused.
    /// </summary>
    /// <remarks>
    /// Signing into a campaign adds the campaign's virtual queue to the agent's queues, and that id is longer than
    /// the original 26 characters. SQL Server refused the membership row, which failed the agent's whole sign-in.
    /// SQLite stores every text column as unbounded <c>TEXT</c>, so it never refused the row and the rebuild is a
    /// value-preserving no-op there. SQLite also refuses to drop a column an index refers to, so the queue index
    /// comes down before the rebuild and is recreated over the widened column.
    /// </remarks>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        // On an engine that resolves an index by name alone the data layer's drop cannot see an index that belongs
        // to a named schema, so it silently drops nothing and the recreation below fails. The qualified drop runs
        // first and is a no-op wherever the data layer's own statement is already sufficient.
        var qualifiedIndexName = SchemaQualifiedIndexDrop.TryGetQualifiedIndexName(
            SchemaBuilder,
            _store,
            typeof(AgentQueueMembershipIndex),
            "IDX_AgentQueueMembershipIndex_Queue",
            ContactCenterStorage.CollectionName);

        if (qualifiedIndexName is not null)
        {
            await using var command = SchemaBuilder.Connection.CreateCommand();
            command.Transaction = SchemaBuilder.Transaction;
            command.CommandText = "drop index if exists " + qualifiedIndexName;

            await command.ExecuteNonQueryAsync();
        }

        // Tolerant because MySQL commits each schema change on its own and writes this drop without IF EXISTS, so an
        // attempt that stopped part-way would otherwise fail every activation from here on. The recreation below
        // runs on the strict builder, so an index that genuinely survived is still reported.
        var tolerantSchemaBuilder = new SchemaBuilder(
            _store.Configuration,
            SchemaBuilder.Transaction,
            throwOnError: false);

        await tolerantSchemaBuilder.AlterIndexTableAsync<AgentQueueMembershipIndex>(
            table => table.DropIndex("IDX_AgentQueueMembershipIndex_Queue"),
            collection: ContactCenterStorage.CollectionName);

        await IndexStringColumnRebuild.WidenAsync<AgentQueueMembershipIndex>(
            SchemaBuilder,
            _store,
            "QueueId",
            ContactCenterStorage.QueueIdLength,
            isNotNull: false,
            defaultValue: null,
            ContactCenterStorage.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<AgentQueueMembershipIndex>(table => table
            .CreateIndex(
                "IDX_AgentQueueMembershipIndex_Queue",
                "DocumentId",
                "QueueId",
                "PresenceStatus",
                "ItemId"),
            collection: ContactCenterStorage.CollectionName);

        return 2;
    }
}
