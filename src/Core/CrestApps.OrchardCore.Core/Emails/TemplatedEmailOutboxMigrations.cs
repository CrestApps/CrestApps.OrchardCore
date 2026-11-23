using CrestApps.OrchardCore.WorkQueues;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Core.Emails;

/// <summary>
/// Creates the index table of the templated email outbox. <c>AddTemplatedEmailOutbox</c> registers it once, with the
/// first feature that calls it.
/// </summary>
public sealed class TemplatedEmailOutboxMigrations : DataMigration
{
    /// <summary>
    /// Creates the index table.
    /// </summary>
    /// <returns>The version of the schema.</returns>
    public async Task<int> CreateAsync()
    {
        await TemplatedEmailOutboxSchema.CreateAsync(SchemaBuilder);

        return 1;
    }
}

/// <summary>
/// The schema of the templated email outbox's index table.
/// </summary>
public static class TemplatedEmailOutboxSchema
{
    /// <summary>
    /// Creates the index table and the indexes due, finished, record and address queries use.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the migration.</param>
    /// <returns>A task that completes when the table is created.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<TemplatedEmailOutboxIndex>(table => table
            .Column<string>(nameof(TemplatedEmailOutboxIndex.ItemId), column => column.WithLength(26))
            .Column<LeasedWorkItemStatus>(nameof(TemplatedEmailOutboxIndex.Status))
            .Column<DateTime>(nameof(TemplatedEmailOutboxIndex.NextAttemptUtc), column => column.NotNull())
            .Column<string>(nameof(TemplatedEmailOutboxIndex.Source), column => column.Nullable().WithLength(TemplatedEmailOutboxIndexProvider.SourceLength))
            .Column<string>(nameof(TemplatedEmailOutboxIndex.Recipient), column => column.Nullable().WithLength(254))
            .Column<DateTime>(nameof(TemplatedEmailOutboxIndex.FinishedUtc), column => column.Nullable()),
            collection: TemplatedEmailOutboxStorage.CollectionName);

        await schemaBuilder.AlterIndexTableAsync<TemplatedEmailOutboxIndex>(table =>
        {
            table.CreateIndex("IDX_TemplatedEmailOutbox_Due", "DocumentId", nameof(TemplatedEmailOutboxIndex.Status), nameof(TemplatedEmailOutboxIndex.NextAttemptUtc));
            table.CreateIndex("IDX_TemplatedEmailOutbox_ItemId", nameof(TemplatedEmailOutboxIndex.ItemId), "DocumentId");
            table.CreateIndex("IDX_TemplatedEmailOutbox_Finished", nameof(TemplatedEmailOutboxIndex.Status), nameof(TemplatedEmailOutboxIndex.FinishedUtc));
            table.CreateIndex("IDX_TemplatedEmailOutbox_Source", nameof(TemplatedEmailOutboxIndex.Source));
            table.CreateIndex("IDX_TemplatedEmailOutbox_Recipient", nameof(TemplatedEmailOutboxIndex.Recipient));
        }, collection: TemplatedEmailOutboxStorage.CollectionName);
    }
}
