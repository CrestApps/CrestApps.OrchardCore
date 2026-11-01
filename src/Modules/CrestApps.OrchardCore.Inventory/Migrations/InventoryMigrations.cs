using CrestApps.OrchardCore.Inventory.Core;
using CrestApps.OrchardCore.Inventory.Core.Indexes;
using CrestApps.OrchardCore.Inventory.Core.Models;
using CrestApps.OrchardCore.Inventory.Core.Services;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using OrchardCore.Entities;
using OrchardCore.Environment.Shell.Scope;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Microsoft.Extensions.DependencyInjection;
using YesSql;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Inventory.Migrations;

internal sealed class InventoryMigrations : DataMigration
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

        await CreateReportIndexAsync();
        await CreateStockLocationsAsync();

        return 3;
    }

    /// <summary>
    /// Adds the index reports read stock movements through, newest first by <c>Utc</c> a page at a time, so each page and
    /// each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the stock location store and the references of stock movements, and moves the locations kept in the
    /// settings into the store with their ids, once the migration has run.
    /// </summary>
    /// <returns>The migration version.</returns>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateStockLocationsAsync();

        ShellScope.AddDeferredTask(async scope =>
        {
            var siteService = scope.ServiceProvider.GetRequiredService<ISiteService>();
            var site = await siteService.LoadSiteSettingsAsync();
            var settings = site.GetOrCreate<InventorySettings>();

            await StockLocationImporter.ImportAsync(settings.Locations ?? [], scope.ServiceProvider.GetRequiredService<StockLocationStore>(), scope.ServiceProvider.GetRequiredService<IClock>().UtcNow);
            await scope.ServiceProvider.GetRequiredService<ISession>().SaveChangesAsync();

            settings.Locations = [];
            site.Put(settings);
            await siteService.UpdateSiteSettingsAsync(site);
        });

        return 3;
    }

    private async Task CreateStockLocationsAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<StockLocationIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<string>("OwnerKind", column => column.Nullable().WithLength(64))
            .Column<string>("OwnerId", column => column.Nullable().WithLength(26)),
            collection: InventoryConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<StockLocationIndex>(table =>
        {
            table.CreateIndex("IDX_StockLocationIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_StockLocationIndex_Owner", "OwnerKind", "OwnerId");
        }, collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<StockMovementIndex>(table =>
        {
            table.AddColumn<string>("ReferenceType", column => column.Nullable().WithLength(64));
            table.AddColumn<string>("ReferenceId", column => column.Nullable().WithLength(64));
        }, collection: InventoryConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<StockMovementIndex>(table => table.CreateIndex("IDX_StockMovementIndex_Reference", "ReferenceType", "ReferenceId"), collection: InventoryConstants.CollectionName);
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<StockMovementIndex>(
            SchemaBuilder,
            "IDX_StockMovementIndex_Report",
            InventoryConstants.CollectionName,
            "Utc", "Id");
    }
}
