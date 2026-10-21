using CrestApps.OrchardCore.Companies.Indexes;
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

        await SchemaBuilder.CreateMapIndexTableAsync<PriceListIndex>(table => table
            .Column<string>("ItemId", column => column.WithLength(26))
            .Column<string>("Name", column => column.WithLength(255)),
            collection: CompaniesConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CompanyIndex>(table => table
            .CreateIndex("IDX_CompanyIndex_ItemId", "ItemId"),
            collection: CompaniesConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<CompanyBuyerIndex>(table => table
            .CreateIndex("IDX_CompanyBuyerIndex_UserId", "UserId"),
            collection: CompaniesConstants.CollectionName
        );

        await SchemaBuilder.AlterIndexTableAsync<PriceListIndex>(table => table
            .CreateIndex("IDX_PriceListIndex_ItemId", "ItemId"),
            collection: CompaniesConstants.CollectionName
        );

        return 1;
    }
}
