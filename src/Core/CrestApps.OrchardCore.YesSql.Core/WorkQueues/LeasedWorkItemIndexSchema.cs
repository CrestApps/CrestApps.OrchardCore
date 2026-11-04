using CrestApps.OrchardCore.WorkQueues;
using YesSql.Sql;

namespace CrestApps.OrchardCore.YesSql.Core.WorkQueues;

/// <summary>
/// Creates the <see cref="LeasedWorkItemIndex"/> table of a collection, for the migration of the feature that owns the
/// collection.
/// </summary>
public static class LeasedWorkItemIndexSchema
{
    /// <summary>
    /// Creates the index table and the index due items are found by.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder of the migration.</param>
    /// <param name="collectionName">The collection, or <see langword="null"/> for the default one.</param>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder, string collectionName)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        await schemaBuilder.CreateMapIndexTableAsync<LeasedWorkItemIndex>(table => table
            .Column<string>(nameof(LeasedWorkItemIndex.ItemId), column => column.WithLength(26))
            .Column<LeasedWorkItemStatus>(nameof(LeasedWorkItemIndex.Status))
            .Column<DateTime>(nameof(LeasedWorkItemIndex.NextAttemptUtc), column => column.NotNull()),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<LeasedWorkItemIndex>(table => table
            .CreateIndex(
                "IDX_LeasedWorkItemIndex_Due",
                "DocumentId",
                nameof(LeasedWorkItemIndex.Status),
                nameof(LeasedWorkItemIndex.NextAttemptUtc)),
            collection: collectionName);

        await schemaBuilder.AlterIndexTableAsync<LeasedWorkItemIndex>(table => table
            .CreateIndex(
                "IDX_LeasedWorkItemIndex_ItemId",
                nameof(LeasedWorkItemIndex.ItemId),
                "DocumentId"),
            collection: collectionName);
    }
}
