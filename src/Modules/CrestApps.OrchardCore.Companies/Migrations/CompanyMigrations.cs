using CrestApps.OrchardCore.Companies.Indexes;
using CrestApps.OrchardCore.YesSql.Core.Migrations;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Companies.Migrations;

/// <summary>
/// Creates the indexes of companies, their buyers and price lists.
/// </summary>
public sealed class CompanyMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<CompanyIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255))
            .Column<bool>("IsEnabled"),
            collection: CompaniesConstants.CollectionName
        );

        await SchemaBuilder.CreateMapIndexTableAsync<CompanyBuyerIndex>(table => table
            .Column<string>("CompanyId", column => column.WithLength(26))
            .Column<string>("UserId", column => column.WithLength(26)),
            collection: CompaniesConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<CompanyIndex>(SchemaBuilder, "IDX_CompanyIndex_ItemId", CompaniesConstants.CollectionName, "ItemId");
        await IndexTableIndexCreation.CreateIfMissingAsync<CompanyBuyerIndex>(SchemaBuilder, "IDX_CompanyBuyerIndex_UserId", CompaniesConstants.CollectionName, "UserId");
        await CreatePriceListIndexAsync();

        return 2;
    }

    public async Task<int> UpdateFrom1Async()
    {
        await CreatePriceListIndexAsync();

        return 2;
    }

    private async Task CreatePriceListIndexAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<PriceListIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: CompaniesConstants.CollectionName
        );

        await IndexTableIndexCreation.CreateIfMissingAsync<PriceListIndex>(SchemaBuilder, "IDX_PriceListIndex_ItemId", CompaniesConstants.CollectionName, "ItemId");
    }
}
