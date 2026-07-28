using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using CrestApps.OrchardCore.ContactCenter.Models;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionIndex"/>.
/// </summary>
internal sealed class InteractionIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the interaction index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InteractionIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<InteractionChannel>("Channel")
            .Column<InteractionDirection>("Direction")
            .Column<InteractionStatus>("Status")
            .Column<string>("ActivityItemId", column => column.WithLength(26))
            .Column<string>("ProviderName", column => column.WithLength(128))
            .Column<string>("ProviderInteractionId", column => column.WithLength(128))
            .Column<string>("ProviderLegId", column => column.WithLength(128))
            .Column<string>("QueueId", column => column.WithLength(26))
            .Column<string>("AgentId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc", column => column.NotNull())
            .Column<DateTime>("EndedUtc"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_DocumentId",
                "DocumentId",
                "ItemId",
                "Status",
                "QueueId",
                "AgentId"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex("IDX_InteractionIndex_Lookup",
                "ActivityItemId",
                "ProviderInteractionId",
                "ProviderLegId",
                "CorrelationId"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }

    /// <summary>
    /// Adds after-call wrap-up timestamps used by handle-time reporting.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table =>
        {
            table.AddColumn<DateTime>("WrapUpStartedUtc");
            table.AddColumn<DateTime>("WrapUpCompletedUtc");
        },
            collection: ContactCenterConstants.CollectionName
        );

        return 2;
    }

    /// <summary>
    /// Adds the covering index the retention purge scans. Without it every terminating batch of the drain loop
    /// is a full scan of a table that grows with traffic, which is exactly the table size retention exists for.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex(
                "IDX_InteractionIndex_Retention",
                "EndedUtc",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        return 3;
    }

    /// <summary>
    /// Adds the predicate-led index the reservation path reads. Routing asks how much live work an agent already
    /// holds before every offer, and the existing composite leads with <c>DocumentId</c>, which serves join-back
    /// and delete-by-document but answers nothing about an agent: without this index the question is answered by
    /// scanning every interaction the contact center has ever recorded, on every routing decision.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<InteractionIndex>(table => table
            .CreateIndex(
                "IDX_InteractionIndex_ActiveByAgent",
                "AgentId",
                "Status",
                "DocumentId"),
            collection: ContactCenterConstants.CollectionName);

        return 4;
    }
}
