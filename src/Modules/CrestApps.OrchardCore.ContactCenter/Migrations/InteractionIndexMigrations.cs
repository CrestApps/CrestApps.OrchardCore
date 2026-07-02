using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
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
}
