using CrestApps.OrchardCore.Markets.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Markets.Migrations;

/// <summary>
/// Creates the index of markets.
/// </summary>
public sealed class MarketMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<MarketIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<string>("Currency", column => column.WithLength(3))
            .Column<bool>("IsEnabled"),
            collection: MarketsConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<MarketIndex>(SchemaBuilder, "IDX_MarketIndex_ItemId", MarketsConstants.CollectionName, "ItemId");

        return 1;
    }
}
