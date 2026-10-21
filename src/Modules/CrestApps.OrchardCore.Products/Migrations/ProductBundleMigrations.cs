using CrestApps.OrchardCore.Products.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the table of the products each bundle is made of. No bundle exists before the feature, so it starts
/// empty.
/// </summary>
internal sealed class ProductBundleMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductBundleIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("PartContentItemId", column => column.WithLength(26))
            .Column<int>("Quantity")
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<ProductBundleIndex>(table => table
            .CreateIndex("IDX_ProductBundleIndex_Bundle", "ContentItemId", "Published", "DocumentId"));

        await SchemaBuilder.AlterIndexTableAsync<ProductBundleIndex>(table => table
            .CreateIndex("IDX_ProductBundleIndex_Part", "PartContentItemId", "Published", "DocumentId"));

        return 1;
    }
}
