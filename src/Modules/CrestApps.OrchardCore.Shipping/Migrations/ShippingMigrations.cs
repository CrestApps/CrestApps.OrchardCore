using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Shipping.Migrations;

public sealed class ShippingMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public ShippingMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await _contentDefinitionManager.AlterPartDefinitionAsync(ShippingConstants.Parts.ShippingPart, part => part
            .Attachable()
            .WithDisplayName("Shipping")
            .WithDescription("A product's weight, the weight of each variant, and its shipping class, for shipping rates."));

        return 1;
    }
}
