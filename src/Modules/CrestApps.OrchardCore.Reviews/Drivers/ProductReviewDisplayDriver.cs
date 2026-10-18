using CrestApps.OrchardCore.Reviews.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Reviews.Drivers;

/// <summary>
/// A review as a row of the moderation list, in both admin list layouts.
/// </summary>
internal sealed class ProductReviewDisplayDriver : DisplayDriver<ProductReview>
{
    public override Task<IDisplayResult> DisplayAsync(ProductReview review, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return CombineAsync(
            View("ProductReview_Fields_SummaryAdmin", review)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("ProductReview_Body_SummaryAdmin", review)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(review.Title) || !string.IsNullOrEmpty(review.Body))),
            View("ProductReview_Status_SummaryAdmin", review)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("ProductReview_Meta_SummaryAdmin", review)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Rating:1"),
            View("ProductReview_Buttons_SummaryAdmin", review).Location("Actions:5"));
    }
}
