using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Indexes;
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
public sealed class ProductListingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductListingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductSearchIndex>(table => table
            .Column<string>(nameof(ProductSearchIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ProductSearchIndex.ContentType), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>(nameof(ProductSearchIndex.Title), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>(nameof(ProductSearchIndex.Sku), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<decimal?>(nameof(ProductSearchIndex.Price), column => column.Nullable())
            .Column<bool>(nameof(ProductSearchIndex.IsPurchasable))
            .Column<DateTime?>(nameof(ProductSearchIndex.PublishedUtc), column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .CreateIndex("IDX_ProductSearchIndex_ContentItemId", "DocumentId", nameof(ProductSearchIndex.ContentItemId))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .CreateIndex("IDX_ProductSearchIndex_ContentType", "DocumentId", nameof(ProductSearchIndex.ContentType), nameof(ProductSearchIndex.PublishedUtc))
        );

        await SchemaBuilder.CreateMapIndexTableAsync<ProductAttributeIndex>(table => table
            .Column<string>(nameof(ProductAttributeIndex.ContentItemId), column => column.WithLength(26))
            .Column<string>(nameof(ProductAttributeIndex.ContentType), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>(nameof(ProductAttributeIndex.AttributeKey), column => column.WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<string>(nameof(ProductAttributeIndex.TextValue), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<decimal?>(nameof(ProductAttributeIndex.NumberValue), column => column.Nullable())
            .Column<DateTime?>(nameof(ProductAttributeIndex.DateValue), column => column.Nullable())
            .Column<string>(nameof(ProductAttributeIndex.Label), column => column.Nullable().WithLength(ProductSearchIndexProvider.MaxTextLength))
            .Column<int>(nameof(ProductAttributeIndex.Level))
            .Column<int>(nameof(ProductAttributeIndex.SortOrder))
            .Column<bool>(nameof(ProductAttributeIndex.Searchable))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductAttributeIndex>(table => table
            .CreateIndex("IDX_ProductAttributeIndex_Text", nameof(ProductAttributeIndex.AttributeKey), nameof(ProductAttributeIndex.TextValue), "DocumentId")
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductAttributeIndex>(table => table
            .CreateIndex("IDX_ProductAttributeIndex_Number", nameof(ProductAttributeIndex.AttributeKey), nameof(ProductAttributeIndex.NumberValue), "DocumentId")
        );

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

    /// <summary>
    /// Adds the culture of localized products, so a listing shows the products of its own culture.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .AddColumn<string>(nameof(ProductSearchIndex.Culture), column => column.Nullable().WithLength(32))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .CreateIndex("IDX_ProductSearchIndex_Culture", "DocumentId", nameof(ProductSearchIndex.Culture))
        );

        return 2;
    }

    /// <summary>
    /// Adds the merchant's featured flag and position, which the "Featured first" and "Recommended" sorts order by.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .AddColumn<bool>(nameof(ProductSearchIndex.Featured), column => column.WithDefault(false))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSearchIndex>(table => table
            .AddColumn<int?>(nameof(ProductSearchIndex.Position), column => column.Nullable())
        );

        return 3;
    }

    /// <summary>
    /// Adds when products' sales start and end, so their records are built again at those moments.
    /// </summary>
    public async Task<int> UpdateFrom3Async()
    {
        await SchemaBuilder.CreateMapIndexTableAsync<ProductSaleIndex>(table => table
            .Column<string>(nameof(ProductSaleIndex.ContentItemId), column => column.WithLength(26))
            .Column<DateTime?>(nameof(ProductSaleIndex.StartsUtc), column => column.Nullable())
            .Column<DateTime?>(nameof(ProductSaleIndex.EndsUtc), column => column.Nullable())
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSaleIndex>(table => table
            .CreateIndex("IDX_ProductSaleIndex_StartsUtc", "DocumentId", nameof(ProductSaleIndex.StartsUtc))
        );

        await SchemaBuilder.AlterIndexTableAsync<ProductSaleIndex>(table => table
            .CreateIndex("IDX_ProductSaleIndex_EndsUtc", "DocumentId", nameof(ProductSaleIndex.EndsUtc))
        );

        return 4;
    }

    // Mirrors the Autoroute module's settings by name, so the type can be defined without a reference to it.
    private sealed class AutoroutePartSettings
    {
        public bool AllowCustomPath { get; set; }

        public string Pattern { get; set; }
    }
}
