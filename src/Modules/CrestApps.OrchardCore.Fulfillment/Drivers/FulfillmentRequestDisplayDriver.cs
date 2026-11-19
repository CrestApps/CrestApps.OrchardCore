using CrestApps.OrchardCore.Fulfillment.Core.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Fulfillment.Drivers;

/// <summary>
/// A row of the Commerce › Fulfillment requests list.
/// </summary>
internal sealed class FulfillmentRequestDisplayDriver : DisplayDriver<FulfillmentRequest>
{
    public override IDisplayResult Display(FulfillmentRequest request, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
        }

        return Combine(
            View("FulfillmentRequest_Fields_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Order:1"),
            View("FulfillmentRequest_Description_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Order:5"),
            View("FulfillmentRequest_Status_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("FulfillmentRequest_Partner_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Partner:1"),
            View("FulfillmentRequest_Meta_SummaryAdmin", request)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Changed:1"),
            View("FulfillmentRequest_Buttons_SummaryAdmin", request)
                .Location("Actions:5")
                .RenderWhen(() => Task.FromResult(request.Status is not (FulfillmentRequestStatus.Sent or FulfillmentRequestStatus.Sending))));
    }
}
