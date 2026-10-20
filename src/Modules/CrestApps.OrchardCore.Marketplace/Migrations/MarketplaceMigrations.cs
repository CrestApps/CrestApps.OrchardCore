using CrestApps.OrchardCore.Marketplace.Indexes;
using CrestApps.OrchardCore.Marketplace.Models;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
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

        await IndexTableIndexCreation.CreateIfMissingAsync<VendorUserIndex>(SchemaBuilder, "IDX_VendorUserIndex_User", MarketplaceConstants.CollectionName, "UserId");
        await IndexTableIndexCreation.CreateIfMissingAsync<VendorEarningIndex>(SchemaBuilder, "IDX_VendorEarningIndex_Vendor", MarketplaceConstants.CollectionName, "VendorId", "Status");
        await IndexTableIndexCreation.CreateIfMissingAsync<VendorEarningIndex>(SchemaBuilder, "IDX_VendorEarningIndex_Order", MarketplaceConstants.CollectionName, "OrderId");
        await IndexTableIndexCreation.CreateIfMissingAsync<VendorEarningIndex>(SchemaBuilder, "IDX_VendorEarningIndex_Hold", MarketplaceConstants.CollectionName, "Status", "HoldUntilUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<VendorPayoutIndex>(SchemaBuilder, "IDX_VendorPayoutIndex_Vendor", MarketplaceConstants.CollectionName, "VendorId");

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(VendorPart), part => part
            .Attachable()
            .WithDisplayName("Vendor")
            .WithDescription("Names the marketplace vendor a product is sold for, who's paid for its sales less the store's commission.")
        );

        return 1;
    }
}
