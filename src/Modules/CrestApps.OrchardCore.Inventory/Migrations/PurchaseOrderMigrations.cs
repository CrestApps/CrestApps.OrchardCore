using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

/// <summary>
/// Creates the table purchase orders are found by.
/// </summary>
internal sealed class PurchaseOrderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PurchaseOrderIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Number", column => column.WithLength(32))
            .Column<string>("NormalizedSupplier", column => column.Nullable().WithLength(255))
            .Column<int>("Status")
            .Column<string>("LocationId", column => column.Nullable().WithLength(26))
            .Column<DateTime?>("ExpectedUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PurchaseOrderIndex>(table =>
        {
            table.CreateIndex("IDX_PurchaseOrderIndex_Status", "Status", "CreatedUtc");
            table.CreateIndex("IDX_PurchaseOrderIndex_Number", "Number");
        }, collection: InventoryConstants.CollectionName);

        return 1;
    }
}
