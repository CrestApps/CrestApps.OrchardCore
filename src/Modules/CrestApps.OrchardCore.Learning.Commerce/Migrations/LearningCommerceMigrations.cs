using CrestApps.OrchardCore.Learning.Commerce.Core.Indexes;
using CrestApps.OrchardCore.Learning.Commerce.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;
using OrchardCore.Data.Migration;

namespace CrestApps.OrchardCore.Learning.Commerce.Migrations;

/// <summary>
/// Creates the tables of Learning commerce and makes courses and learning paths products.
/// </summary>
internal sealed class LearningCommerceMigrations : DataMigration
{
    private readonly IContentDefinitionManager _contentDefinitionManager;

    public LearningCommerceMigrations(IContentDefinitionManager contentDefinitionManager)
    {
        _contentDefinitionManager = contentDefinitionManager;
    }

    public async Task<int> CreateAsync()
    {
        await LearningCommerceIndexSchema.CreateAsync(SchemaBuilder);

        await _contentDefinitionManager.AlterPartDefinitionAsync(nameof(LearningProductPart), part => part
            .Attachable()
            .WithDisplayName("Learning Product")
            .WithDescription("What buying the product grants: the course or path itself, other courses, or seats for an account."));

        await LearningCommerceProductTypes.SellOnTheirPagesAsync(_contentDefinitionManager);

        return 3;
    }

    // The work queue that fulfils paid purchases after the checkout: a new table only.
    public async Task<int> UpdateFrom1Async()
    {
        await LearningCommerceIndexSchema.CreateFulfillmentQueueAsync(SchemaBuilder);

        return 2;
    }

    // A course or a path without a price is simply not for sale, and a priced one shows its price and Add to cart on its
    // page: sites made before this step required a price on every course, and some had a path without the details.
    public async Task<int> UpdateFrom2Async()
    {
        await LearningCommerceProductTypes.SellOnTheirPagesAsync(_contentDefinitionManager);

        return 3;
    }
}
