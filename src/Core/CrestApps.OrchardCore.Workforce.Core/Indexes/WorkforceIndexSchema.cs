using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Workforce.Models;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Workforce.Core.Indexes;

/// <summary>
/// Creates the Workforce index tables, for the module's migration and the tests.
/// </summary>
public static class WorkforceIndexSchema
{
    /// <summary>
    /// Creates the index tables in the <see cref="WorkforceConstants.CollectionName"/> collection.
    /// </summary>
    /// <param name="schemaBuilder">The schema builder.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAsync(ISchemaBuilder schemaBuilder)
    {
        ArgumentNullException.ThrowIfNull(schemaBuilder);

        const string collection = WorkforceConstants.CollectionName;

        await schemaBuilder.CreateMapIndexTableAsync<ActivityCodeIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<ActivityKind>("Kind")
            .Column<bool>("Enabled"),
            collection: collection);

        await schemaBuilder.AlterIndexTableAsync<ActivityCodeIndex>(table => table.CreateIndex("IDX_ActivityCodeIndex_ItemId", "ItemId"), collection: collection);
    }
}
