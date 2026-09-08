using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Products;

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

        return 2;
    }

    /// <summary>
    /// Adds the part that lets one product be offered at several prices.
    /// </summary>
    public async Task<int> UpdateFrom1Async()
    {
        await CreatePricePartAsync();

        return 2;
    }

    private Task CreatePricePartAsync()
        => _contentDefinitionManager.AlterPartDefinitionAsync("ProductPricePart", part => part
            .Attachable()
            .WithDisplayName("Product prices")
            .WithDescription("Offers a product at more than one price, one-time or recurring.")
        );
}
