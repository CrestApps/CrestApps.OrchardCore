using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

/// <summary>
/// Creates the table purchase orders are found by.
/// </summary>
public sealed class PurchaseOrderMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PurchaseOrderIndex>(table => table
            .Column<string>(nameof(PurchaseOrderIndex.ItemId), column => column.WithLength(26))
            .Column<string>(nameof(PurchaseOrderIndex.Number), column => column.WithLength(32))
            .Column<string>(nameof(PurchaseOrderIndex.NormalizedSupplier), column => column.Nullable().WithLength(255))
            .Column<int>(nameof(PurchaseOrderIndex.Status))
            .Column<string>(nameof(PurchaseOrderIndex.LocationId), column => column.Nullable().WithLength(26))
            .Column<DateTime?>(nameof(PurchaseOrderIndex.ExpectedUtc), column => column.Nullable())
            .Column<DateTime>(nameof(PurchaseOrderIndex.CreatedUtc)),
            collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<PurchaseOrderIndex>(table =>
        {
            table.CreateIndex("IDX_PurchaseOrderIndex_Status", nameof(PurchaseOrderIndex.Status), nameof(PurchaseOrderIndex.CreatedUtc));
            table.CreateIndex("IDX_PurchaseOrderIndex_Number", nameof(PurchaseOrderIndex.Number));
        }, collection: InventoryConstants.CollectionName);

        return 1;
    }
}
