using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Products.Feeds.Migrations;

/// <summary>
/// Defines the Product Feed part, to add to the product types that need a brand, GTIN or MPN in feeds.
/// </summary>
internal sealed class ProductFeedMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ProductFeedMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(ProductFeedPart), part => part
            .Attachable()
            .WithDisplayName("Product Feed")
            .WithDescription("A product's brand, GTIN, MPN, condition and Google category for shopping feeds, or that it stays out of them."));

        return 1;
    }
}
