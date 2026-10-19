using CrestApps.OrchardCore.Shipping.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Shipping.Migrations;

public sealed class ShippingMigrations : DataMigration
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

        await CreateShipmentIndexAsync();

        return 3;
    }

    /// <summary>
    /// Adds shipments.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreateShipmentIndexAsync();

        return 3;
    }

    /// <summary>
    /// Finds shipments by tracking number, for the carriers' tracking updates.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ShipmentIndex>(table => table
            .AddColumn<string>("TrackingNumber", column => column.Nullable().WithLength(64)),
            collection: ShippingConstants.CollectionName);

        await IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(SchemaBuilder, "IDX_ShipmentIndex_TrackingNumber", ShippingConstants.CollectionName, "TrackingNumber");

        return 3;
    }

    private async Task CreateShipmentIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ShipmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("CreatedUtc")
            .Column<DateTime>("ShippedUtc", column => column.Nullable())
            .Column<string>("TrackingNumber", column => column.Nullable().WithLength(64)),
            collection: ShippingConstants.CollectionName
        );

        // Safe to run again: a server that finished the step first has already made them.
        await IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(SchemaBuilder, "IDX_ShipmentIndex_ItemId", ShippingConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(SchemaBuilder, "IDX_ShipmentIndex_OrderId", ShippingConstants.CollectionName, "OrderId", "CreatedUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(SchemaBuilder, "IDX_ShipmentIndex_Status_ShippedUtc", ShippingConstants.CollectionName, "Status", "ShippedUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<ShipmentIndex>(SchemaBuilder, "IDX_ShipmentIndex_TrackingNumber", ShippingConstants.CollectionName, "TrackingNumber");
    }
}
