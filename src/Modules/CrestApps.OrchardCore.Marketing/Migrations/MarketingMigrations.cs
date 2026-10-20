using CrestApps.OrchardCore.Marketing.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Marketing.Migrations;

/// <summary>
/// Creates the indexes of customers' purchases, segments and campaigns.
/// </summary>
public sealed class MarketingMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CustomerPurchasesIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<int>("OrderCount")
            .Column<decimal>("TotalSpent")
            .Column<DateTime>("FirstOrderUtc", column => column.Nullable())
            .Column<DateTime>("LastOrderUtc", column => column.Nullable())
            .Column<string>("Country", column => column.Nullable().WithLength(2)),
            collection: MarketingConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<SegmentIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: MarketingConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<CampaignIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<int>("Status")
            .Column<DateTime>("ScheduledUtc", column => column.Nullable())
            .Column<DateTime>("CreatedUtc"),
            collection: MarketingConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<CustomerPurchasesIndex>(SchemaBuilder, "IDX_CustomerPurchasesIndex_ItemId", MarketingConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<CustomerPurchasesIndex>(SchemaBuilder, "IDX_CustomerPurchasesIndex_LastOrder", MarketingConstants.CollectionName, "LastOrderUtc");
        await IndexTableIndexCreation.CreateIfMissingAsync<CampaignIndex>(SchemaBuilder, "IDX_CampaignIndex_Status", MarketingConstants.CollectionName, "Status", "ScheduledUtc");

        return 1;
    }
}
