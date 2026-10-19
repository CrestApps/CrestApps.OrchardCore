using CrestApps.OrchardCore.Products.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the table of the products each bundle is made of. No bundle exists before the feature, so it starts
/// empty.
/// </summary>
public sealed class ProductBundleMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductBundleIndex>(table => table
            .Column<string>(nameof(ProductBundleIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ProductBundleIndex.PartContentItemId), column => column.WithLength(26))
            .Column<int>(nameof(ProductBundleIndex.Quantity))
            .Column<bool>(nameof(ProductBundleIndex.Published))
            .Column<bool>(nameof(ProductBundleIndex.Latest)));

        await SchemaBuilder.AlterIndexTableAsync<ProductBundleIndex>(table => table
            .CreateIndex("IDX_ProductBundleIndex_Bundle", "DocumentId", nameof(ProductBundleIndex.ContentItemId), nameof(ProductBundleIndex.Published)));

        await SchemaBuilder.AlterIndexTableAsync<ProductBundleIndex>(table => table
            .CreateIndex("IDX_ProductBundleIndex_Part", "DocumentId", nameof(ProductBundleIndex.PartContentItemId), nameof(ProductBundleIndex.Published)));

        return 1;
    }
}
