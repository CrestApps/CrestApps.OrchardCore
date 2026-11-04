using CrestApps.OrchardCore.Outreach.Models;
using CrestApps.OrchardCore.WorkQueues;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Outreach.Core.Indexes;

/// <summary>
/// Creates the outreach index tables, for the Outreach feature's migration.
/// </summary>
public static class OutreachIndexSchema
{
    /// <summary>
    /// Creates the run and batch index tables in the outreach collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the migration.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<OutreachRunIndex>(table => table
            .Column<string>(nameof(OutreachRunIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(OutreachRunIndex.OwnerType), column => column.WithLength(64))
            .Column<string>(nameof(OutreachRunIndex.OwnerId), column => column.WithLength(64))
            .Column<OutreachRunStatus>(nameof(OutreachRunIndex.Status))
            .Column<DateTime>(nameof(OutreachRunIndex.CreatedUtc)),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OutreachRunIndex>(table => table
            .CreateIndex("IDX_OutreachRunIndex_ItemId", nameof(OutreachRunIndex.ItemId), "DocumentId"),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OutreachRunIndex>(table => table
            .CreateIndex("IDX_OutreachRunIndex_Owner", nameof(OutreachRunIndex.OwnerType), nameof(OutreachRunIndex.OwnerId), "DocumentId"),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.CreateMapIndexTableAsync<OutreachBatchIndex>(table => table
            .Column<string>(nameof(OutreachBatchIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(OutreachBatchIndex.RunId), column => column.WithLength(26))
            .Column<LeasedWorkItemStatus>(nameof(OutreachBatchIndex.Status))
            .Column<DateTime>(nameof(OutreachBatchIndex.NextAttemptUtc), column => column.NotNull()),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OutreachBatchIndex>(table => table
            .CreateIndex("IDX_OutreachBatchIndex_Due", "DocumentId", nameof(OutreachBatchIndex.Status), nameof(OutreachBatchIndex.NextAttemptUtc)),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OutreachBatchIndex>(table => table
            .CreateIndex("IDX_OutreachBatchIndex_ItemId", nameof(OutreachBatchIndex.ItemId), "DocumentId"),
            collection: OutreachConstants.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<OutreachBatchIndex>(table => table
            .CreateIndex("IDX_OutreachBatchIndex_RunId", nameof(OutreachBatchIndex.RunId), "DocumentId"),
            collection: OutreachConstants.CollectionName);
    }
}
