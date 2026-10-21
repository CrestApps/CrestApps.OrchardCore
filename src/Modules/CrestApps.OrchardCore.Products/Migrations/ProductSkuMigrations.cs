using CrestApps.OrchardCore.Products.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the table of products' SKUs.
/// </summary>
public sealed class ProductSkuMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductSkuIndex>(table => table
            .Column<string>(nameof(ProductSkuIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ProductSkuIndex.ContentType), column => column.WithLength(255))
            .Column<string>(nameof(ProductSkuIndex.Sku), column => column.WithLength(ProductSkuIndex.MaxSkuLength))
            .Column<string>(nameof(ProductSkuIndex.VariantId), column => column.Nullable().WithLength(26))
            .Column<bool>(nameof(ProductSkuIndex.Published))
            .Column<bool>(nameof(ProductSkuIndex.Latest)));

        await SchemaBuilder.AlterIndexTableAsync<ProductSkuIndex>(table => table
            .CreateIndex("IDX_ProductSkuIndex_Sku", "DocumentId", nameof(ProductSkuIndex.Sku), nameof(ProductSkuIndex.ContentType), nameof(ProductSkuIndex.Latest)));

        return 1;
    }
}
