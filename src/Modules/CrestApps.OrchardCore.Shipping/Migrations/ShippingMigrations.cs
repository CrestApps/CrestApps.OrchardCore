using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Shipping.Migrations;

internal sealed class ShippingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ShippingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(ShippingConstants.Parts.ShippingPart, part => part
            .Attachable()
            .WithDisplayName("Shipping")
            .WithDescription("A product's weight, the weight of each variant, and its shipping class, for shipping rates."));

        await SchemaBuilder.CreateMapIndexTableAsync<ShipmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ShippedUtc", column => column.Nullable())
            .Column<string>("TrackingNumber", column => column.Nullable().WithLength(64)),
            collection: ShippingConstants.CollectionName
        );

        // The carriers' tracking updates find a shipment by its tracking number.
        await SchemaBuilder.AlterIndexTableAsync<ShipmentIndex>(table =>
        {
            table.CreateIndex("IDX_ShipmentIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_ShipmentIndex_OrderId", "OrderId", "CreatedUtc");
            table.CreateIndex("IDX_ShipmentIndex_Status_ShippedUtc", "Status", "ShippedUtc");
            table.CreateIndex("IDX_ShipmentIndex_TrackingNumber", "TrackingNumber");
        }, collection: ShippingConstants.CollectionName);

        await CreateReportIndexAsync();

        return 2;
    }

    /// <summary>
    /// Adds the index reports read shipments through, newest first by <c>CreatedUtc</c> a page at a time, so each page
    /// and each date range is found by seeking.
    /// </summary>
    /// <returns>The migration version number.</returns>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateReportIndexAsync();

        return 2;
    }

    private Task<bool> CreateReportIndexAsync()
    {
        return IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(
            SchemaBuilder,
            "IDX_ShipmentIndex_Report",
            ShippingConstants.CollectionName,
            "CreatedUtc", "Id");
    }
}
