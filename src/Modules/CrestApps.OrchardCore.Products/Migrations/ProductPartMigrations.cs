using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Products.Migrations;

internal sealed class ProductPartMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductPartMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync("ProductPart", part => part
            .Attachable()
            .WithDisplayName("Product")
            .WithDescription("Provides the key properties for any product.")
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("ProductPricePart", part => part
            .Attachable()
            .WithDisplayName("Product prices")
            .WithDescription("Offers a product at more than one price, one-time or recurring.")
        );

        await _contentDefinitionManager.AlterPartDefinitionAsync("ProductVariantsPart", part => part
            .Attachable()
            .WithDisplayName("Product variants")
            .WithDescription("Sells a product in versions, such as sizes and colors, each with its own SKU and price.")
        );

        return 1;
    }
}
