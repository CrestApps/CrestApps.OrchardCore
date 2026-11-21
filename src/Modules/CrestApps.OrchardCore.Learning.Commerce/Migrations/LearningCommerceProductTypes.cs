using CrestApps.OrchardCore.Learning.Commerce.Models;
using CrestApps.OrchardCore.Learning.Programs;
using CrestApps.OrchardCore.Products.Core.Listing;
using CrestApps.OrchardCore.Products.Core.Models;
using OrchardCore.ContentManagement.Metadata;
using OrchardCore.ContentManagement.Metadata.Settings;

namespace CrestApps.OrchardCore.Learning.Commerce.Migrations;

/// <summary>
/// Makes courses and learning paths products sold on their own pages: the product part with the page details on (price
/// and Add to cart), a price that may be left empty (an item without one is simply not for sale), and the Learning
/// product part. Settings the site changed otherwise are kept.
/// </summary>
internal static class LearningCommerceProductTypes
{
    /// <summary>
    /// Applies the product parts to the Course type and, when it exists, the Learning Path type.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <returns>A task.</returns>
    public static async Task SellOnTheirPagesAsync(IContentDefinitionManager contentDefinitionManager)
    {
        foreach (var type in new[] { LearningConstants.ContentTypes.Course, ProgramsConstants.LearningPathContentType })
        {
            await SellOnItsPageAsync(contentDefinitionManager, type);
        }
    }

    /// <summary>
    /// Applies the product parts to one type, when it exists.
    /// </summary>
    /// <param name="contentDefinitionManager">The content definition manager.</param>
    /// <param name="type">The content type.</param>
    /// <returns>A task.</returns>
    public static async Task SellOnItsPageAsync(IContentDefinitionManager contentDefinitionManager, string type)
    {
        if (await contentDefinitionManager.GetTypeDefinitionAsync(type) is null)
        {
            return;
        }

        await contentDefinitionManager.AlterTypeDefinitionAsync(type, definition => definition
            .WithPart(nameof(ProductPart), part => part
                .WithPosition("30")
                .MergeSettings<ProductPageSettings>(settings => settings.ShowDetails = true)
                .MergeSettings<ProductPartSettings>(settings => settings.PriceOptional = true))
            .WithPart(nameof(LearningProductPart), part => part.WithPosition("31")));
    }
}
