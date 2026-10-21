using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

public sealed class InventoryMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public InventoryMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StockItemIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("VariantId", column => column.WithLength(26))
            .Column<string>("LocationId", column => column.WithLength(26))
            .Column<string>("NormalizedSku", column => column.WithLength(255))
            .Column<string>("NormalizedTitle", column => column.WithLength(255))
            .Column<int>("OnHand")
            .Column<int>("Available")
            .Column<bool>("IsLow")
            .Column<bool>("AllowBackorder")
            .Column<bool>("Archived")
            .Column<DateTime>("ModifiedUtc"),
            collection: InventoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<StockItemIndex>(table =>
        {
            table.CreateIndex("IDX_StockItemIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_StockItemIndex_Product", "ContentItemId", "VariantId", "LocationId");
            table.CreateIndex("IDX_StockItemIndex_NormalizedSku", "NormalizedSku");
            table.CreateIndex("IDX_StockItemIndex_Archived_Available", "Archived", "Available");
        }, collection: InventoryConstants.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<StockMovementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StockItemId", column => column.WithLength(26))
            .Column<int>("Kind")
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<DateTime>("Utc"),
            collection: InventoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<StockMovementIndex>(table =>
        {
            table.CreateIndex("IDX_StockMovementIndex_StockItemId_Utc", "StockItemId", "Utc");
            table.CreateIndex("IDX_StockMovementIndex_OrderId", "OrderId");
        }, collection: InventoryConstants.CollectionName);

        await SchemaBuilder.CreateMapIndexTableAsync<StockReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StockItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ExpiresUtc"),
            collection: InventoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<StockReservationIndex>(table =>
        {
            table.CreateIndex("IDX_StockReservationIndex_OrderId", "OrderId");
            table.CreateIndex("IDX_StockReservationIndex_StockItemId_Status", "StockItemId", "Status");
            table.CreateIndex("IDX_StockReservationIndex_Status_ExpiresUtc", "Status", "ExpiresUtc");
        }, collection: InventoryConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync(InventoryConstants.Parts.InventoryPart, part => part
            .Attachable()
            .WithDisplayName("Inventory")
            .WithDescription("Counts how many units of a product, or each of its variants, are on hand."));

        return 1;
    }
}
