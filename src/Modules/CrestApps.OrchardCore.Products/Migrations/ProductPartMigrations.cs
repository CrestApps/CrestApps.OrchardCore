using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Products.Migrations;

public sealed class ProductPartMigrations : DataMigration
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

        await CreatePricePartAsync();
        await CreateVariantsPartAsync();

        return 3;
    }

    /// <summary>
    /// Adds the part that lets one product be offered at several prices.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreatePricePartAsync();

        return 2;
    }

    /// <summary>
    /// Adds the part that sells a product in variants.
    /// </summary>
    public async Task<int> UpdateFrom2Async()
    {
        await CreateVariantsPartAsync();

        return 3;
    }

    private Task CreatePricePartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync("ProductPricePart", part => part
            .Attachable()
            .WithDisplayName("Product prices")
            .WithDescription("Offers a product at more than one price, one-time or recurring.")
        );

    private Task CreateVariantsPartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync("ProductVariantsPart", part => part
            .Attachable()
            .WithDisplayName("Product variants")
            .WithDescription("Sells a product in versions, such as sizes and colors, each with its own SKU and price.")
        );
}
