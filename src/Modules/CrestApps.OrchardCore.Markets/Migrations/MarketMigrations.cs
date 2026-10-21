using CrestApps.OrchardCore.Markets.Indexes;
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

        await SchemaBuilder.AlterIndexTableAsync<MarketIndex>(table => table
            .CreateIndex("IDX_MarketIndex_ItemId", "ItemId"),
            collection: MarketsConstants.CollectionName
        );

        return 1;
    }
}
