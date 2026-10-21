using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Indexes;
using OrchardCore.Autoroute.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;
using YesSql.Sql;

namespace CrestApps.OrchardCore.Products.Migrations;

/// <summary>
/// Creates the product search index tables and the Product Listing content type.
/// </summary>
/// <remarks>
/// The tables start empty. The indexing background task fills them after the feature is enabled, because a
/// rebuild reads every product and must not run inside a migration.
/// </remarks>
internal sealed class ProductListingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductListingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductSearchIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>("Title", column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>("Sku", column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<decimal?>("Price", column => column.Nullable())
            .Column<bool>("IsPurchasable")
            .Column<DateTime?>("PublishedUtc", column => column.Nullable())
            .Column<string>("Culture", column => column.Nullable().WithLength(32))
            .Column<bool>("Featured", column => column.WithDefault(false))
            .Column<int?>("Position", column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table =>
        {
            table.CreateIndex("IDX_ProductSearchIndex_ContentItemId", "ContentItemId", "DocumentId");
            table.CreateIndex("IDX_ProductSearchIndex_ContentType", "ContentType", "PublishedUtc", "DocumentId");
            table.CreateIndex("IDX_ProductSearchIndex_Culture", "Culture", "DocumentId");
        });

        await SchemaBuilder.CreateMapIndexTableAsync<ProductAttributeIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<string>("ContentType", column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>("AttributeKey", column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>("TextValue", column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<decimal?>("NumberValue", column => column.Nullable())
            .Column<DateTime?>("DateValue", column => column.Nullable())
            .Column<string>("Label", column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<int>("Level")
            .Column<int>("SortOrder")
            .Column<bool>("Searchable")
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductAttributeIndex>(table =>
        {
            table.CreateIndex("IDX_ProductAttributeIndex_Text", "AttributeKey", "TextValue", "DocumentId");
            table.CreateIndex("IDX_ProductAttributeIndex_Number", "AttributeKey", "NumberValue", "DocumentId");
        });

        await SchemaBuilder.CreateMapIndexTableAsync<ProductSaleIndex>(table => table
            .Column<string>("ContentItemId", column => column.WithLength(26))
            .Column<DateTime?>("StartsUtc", column => column.Nullable())
            .Column<DateTime?>("EndsUtc", column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSaleIndex>(table =>
        {
            table.CreateIndex("IDX_ProductSaleIndex_StartsUtc", "StartsUtc", "DocumentId");
            table.CreateIndex("IDX_ProductSaleIndex_EndsUtc", "EndsUtc", "DocumentId");
        });

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ProductListingPart), part => part
            .Attachable()
            .WithDisplayName("Product listing")
            .WithDescription("Turns a content item into a product listing page with filters, sorting and grid or list views.")
        );

        await _contentDefinitionManager.AlterTypeDefinitionAsync(ProductConstants.ProductListingContentType, type => type
            .WithDisplayName("Product Listing")
            .Creatable()
            .Listable()
            .Draftable()
            .Versionable()
            .Securable()
            .WithPart("TitlePart", part => part.WithPosition("0"))
            .WithPart("AutoroutePart", part => part
                .WithPosition("1")
                .WithSettings(new AutoroutePartSettings
                {
                    AllowCustomPath = true,
                    Pattern = "{{ ContentItem | display_text | slugify }}",
                }))
            .WithPart(nameof(ProductListingPart), part => part.WithPosition("2"))
        );

        return 1;
    }
}
