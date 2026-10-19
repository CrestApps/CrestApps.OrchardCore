using CrestApps.OrchardCore.Orders.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Orders.Drivers;

/// <summary>
/// Renders a gift card as a row of the gift cards admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class GiftCardDisplayDriver : DisplayDriver<GiftCard>
{
    public override IDisplayResult Display(GiftCard card, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = card.LastCharacters?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("GiftCard_Fields_SummaryAdmin", card)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("GiftCard_Description_SummaryAdmin", card)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(card.RecipientEmail) || !string.IsNullOrEmpty(card.OrderNumber))),
            View("GiftCard_Status_SummaryAdmin", card)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("GiftCard_Balance_SummaryAdmin", card)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Balance:1"),
            View("GiftCard_Expires_SummaryAdmin", card)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Expires:1"),
            View("GiftCard_Buttons_SummaryAdmin", card).Location("Actions:5"));
    }
}
