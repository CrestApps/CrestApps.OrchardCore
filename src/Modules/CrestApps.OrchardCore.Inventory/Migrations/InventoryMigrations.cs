using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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

        foreach (var (name, columns) in new[]
        {
            ("IDX_StockItemIndex_ItemId", "ItemId"),
            ("IDX_StockItemIndex_Product", "ContentItemId,VariantId,LocationId"),
            ("IDX_StockItemIndex_NormalizedSku", "NormalizedSku"),
            ("IDX_StockItemIndex_Archived_Available", "Archived,Available"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<StockItemIndex>(table => table
                .CreateIndex(name, columns.Split(',')),
                collection: InventoryConstants.CollectionName
            );
        }

        await SchemaBuilder.CreateMapIndexTableAsync<StockMovementIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StockItemId", column => column.WithLength(26))
            .Column<int>("Kind")
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<DateTime>("Utc"),
            collection: InventoryConstants.CollectionName
        );

        foreach (var (name, columns) in new[]
        {
            ("IDX_StockMovementIndex_StockItemId_Utc", "StockItemId,Utc"),
            ("IDX_StockMovementIndex_OrderId", "OrderId"),
        })
        {
            await SchemaBuilder.AlterIndexTableAsync<StockMovementIndex>(table => table
                .CreateIndex(name, columns.Split(',')),
                collection: InventoryConstants.CollectionName
            );
        }

        await _contentDefinitionManager.AlterPartDefinitionAsync(InventoryConstants.Parts.InventoryPart, part => part
            .Attachable()
            .WithDisplayName("Inventory")
            .WithDescription("Counts how many units of a product, or each of its variants, are on hand."));

        await CreateReservationIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the holds stock keeps for orders being paid for.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReservationIndexAsync();

        return 2;
    }

    private async Task CreateReservationIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StockReservationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("StockItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("ExpiresUtc"),
            collection: InventoryConstants.CollectionName
        );

        // Safe to run again: a server that finished the step first has already made them.
        await IndexTableIndexCreation.CreateIfMissingAsync<StockReservationIndex>(SchemaBuilder, "IDX_StockReservationIndex_OrderId", InventoryConstants.CollectionName, "OrderId");
        await IndexTableIndexCreation.CreateIfMissingAsync<StockReservationIndex>(SchemaBuilder, "IDX_StockReservationIndex_StockItemId_Status", InventoryConstants.CollectionName, "StockItemId", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<StockReservationIndex>(SchemaBuilder, "IDX_StockReservationIndex_Status_ExpiresUtc", InventoryConstants.CollectionName, "Status", "ExpiresUtc");
    }
}
