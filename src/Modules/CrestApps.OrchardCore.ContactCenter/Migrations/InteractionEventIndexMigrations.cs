using CrestApps.OrchardCore.ContactCenter.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.ContactCenter.Migrations;

/// <summary>
/// Creates the schema for the <see cref="InteractionEventIndex"/>.
/// </summary>
internal sealed class InteractionEventIndexMigrations : DataMigration
{
    /// <summary>
    /// Creates the interaction event index table and its supporting indexes.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<InteractionEventIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("InteractionId", column => column.WithLength(26))
            .Column<string>("EventType", column => column.WithLength(128))
            .Column<string>("AggregateType", column => column.WithLength(128))
            .Column<string>("AggregateId", column => column.WithLength(26))
            .Column<string>("CorrelationId", column => column.WithLength(26))
            .Column<string>("IdempotencyKey", column => column.WithLength(128))
            .Column<DateTime>("OccurredUtc", column => column.NotNull()),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Interaction",
                "InteractionId",
                "OccurredUtc",
                "EventType"),
            collection: ContactCenterConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<InteractionEventIndex>(table => table
            .CreateIndex("IDX_InteractionEventIndex_Idempotency",
                "IdempotencyKey"),
            collection: ContactCenterConstants.CollectionName
        );

        return 1;
    }
}
