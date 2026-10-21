using CrestApps.OrchardCore.Products.Core.Indexes;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the table of products' SKUs.
/// </summary>
internal sealed class ProductSkuMigrations : DataMigration
{
    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductSkuIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(255))
            .Column<string>("Sku", column => column.WithLength(ProductSkuIndex.MaxSkuLength))
            .Column<string>("VariantId", column => column.Nullable().WithLength(26))
            .Column<bool>("Published")
            .Column<bool>("Latest"));

        await SchemaBuilder.AlterIndexTableAsync<ProductSkuIndex>(table => table
            .CreateIndex("IDX_ProductSkuIndex_Sku", "Sku", "ContentType", "Latest", "DocumentId"));

        return 1;
    }
}
