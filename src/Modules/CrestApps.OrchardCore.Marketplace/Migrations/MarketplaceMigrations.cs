using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Marketplace.Migrations;

/// <summary>
/// Creates the indexes of vendors, their earnings and payouts, and the attachable Vendor part.
/// </summary>
internal sealed class MarketplaceMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public MarketplaceMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<VendorIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.Nullable().WithLength(255))
            .Column<int>("Status"),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<VendorUserIndex>(table => table
            .Column<string>("VendorId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26)),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<VendorEarningIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("VendorId", column => column.WithLength(26))
            .Column<string>("OrderId", column => column.WithLength(26))
            .Column<int>("Status")
            .Column<DateTime>("HoldUntilUtc")
            .Column<DateTime>("CreatedUtc"),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<VendorPayoutIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("VendorId", column => column.WithLength(26))
            .Column<DateTime>("CreatedUtc"),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<VendorIndex>(table => table
            .CreateIndex("IDX_VendorIndex_ItemId", "ItemId"),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<VendorUserIndex>(table => table
            .CreateIndex("IDX_VendorUserIndex_User", "UserId"),
            collection: MarketplaceConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<VendorEarningIndex>(table =>
        {
            table.CreateIndex("IDX_VendorEarningIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_VendorEarningIndex_Vendor", "VendorId", "Status");
            table.CreateIndex("IDX_VendorEarningIndex_Order", "OrderId");
            table.CreateIndex("IDX_VendorEarningIndex_Hold", "Status", "HoldUntilUtc");
        }, collection: MarketplaceConstants.CollectionName);

        await SchemaBuilder.AlterIndexTableAsync<VendorPayoutIndex>(table =>
        {
            table.CreateIndex("IDX_VendorPayoutIndex_ItemId", "ItemId");
            table.CreateIndex("IDX_VendorPayoutIndex_Vendor", "VendorId");
        }, collection: MarketplaceConstants.CollectionName);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(VendorPart), part => part
            .Attachable()
            .WithDisplayName("Vendor")
            .WithDescription("Names the marketplace vendor a product is sold for, who's paid for its sales less the store's commission.")
        );

        return 1;
    }
}
