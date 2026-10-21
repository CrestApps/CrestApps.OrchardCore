using CrestApps.OrchardCore.Products.Core.Indexes;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Data.Migration;
using YesSql;
using YesSql.Services;
using YesSql.Sql;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the table of products' SKUs and fills it for the products already saved, which saving each once more
/// indexes. It runs on the migration's own unit of work, so a tenant starting up waits for it rather than racing it.
/// </summary>
public sealed class ProductSkuMigrations : DataMigration
{
    private const int BatchSize = 200;

    private readonly ISession _session;
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductSkuMigrations(ISession session, IContentDefinitionManager contentDefinitionManager)
    {
        _session = session;
        _contentDefinitionManager = contentDefinitionManager;
    }

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

        // The products saved before the table existed.
        var types = (await _contentDefinitionManager.ListTypeDefinitionsAsync())
            .Where(type => type.Parts.Any(part => part.PartDefinition.Name == nameof(ProductPart)))
            .Select(type => type.Name)
            .ToArray();

        if (types.Length > 0)
        {
            for (var skip = 0; ; skip += BatchSize)
            {
                var items = (await _session.Query<ContentItem, ContentItemIndex>(index => index.ContentType.IsIn(types) && (index.Latest || index.Published))
                    .OrderBy(index => index.Id)
                    .Skip(skip)
                    .Take(BatchSize)
                    .ListAsync()).ToList();

                foreach (var item in items)
                {
                    await _session.SaveAsync(item);
                }

                if (items.Count < BatchSize)
                {
                    break;
                }
            }
        }

        return 1;
    }
}
