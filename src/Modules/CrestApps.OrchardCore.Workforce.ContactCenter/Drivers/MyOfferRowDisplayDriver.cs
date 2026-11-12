using CrestApps.OrchardCore.Workforce.ContactCenter.ViewModels;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Workforce.ContactCenter.Drivers;

/// <summary>
/// Displays an offer a worker can take in the My Work list of offers, in both admin list layouts.
/// </summary>
internal sealed class MyOfferRowDisplayDriver : DisplayDriver<MyOfferRow>
{
    /// <inheritdoc/>
    public override IDisplayResult Display(MyOfferRow row, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape shape)
        {
            shape.Classes.Add("item");
        }

        return Combine(
            View("MyOfferRow_Fields_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("MyOfferRow_Description_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(row.Offer.Note))),
            View("MyOfferRow_When_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "When:5"),
            View("MyOfferRow_Places_SummaryAdmin", row)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:5")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Places:5"),
            View("MyOfferRow_Buttons_SummaryAdmin", row).Location("Actions:5"));
    }
}
