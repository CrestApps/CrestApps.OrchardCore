using CrestApps.OrchardCore.Bookings.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Bookings.Drivers;

/// <summary>
/// Renders a booking as a row of the bookings admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class BookingDisplayDriver : DisplayDriver<Booking>
{
    public override IDisplayResult Display(Booking booking, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = (booking.CustomerName + " " + booking.Title).ToLowerInvariant();
        }

        return Combine(
            View("Booking_Fields_SummaryAdmin", booking)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Booking_Description_SummaryAdmin", booking)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "When:1"),
            View("Booking_Status_SummaryAdmin", booking)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Booking_Buttons_SummaryAdmin", booking).Location("Actions:5"));
    }
}
