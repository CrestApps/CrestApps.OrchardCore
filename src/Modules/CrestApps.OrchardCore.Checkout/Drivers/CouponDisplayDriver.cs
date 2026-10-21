using CrestApps.OrchardCore.Checkout.Models;
using OrchardCore;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Checkout.Drivers;

/// <summary>
/// Renders a coupon as a row of the coupons admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class CouponDisplayDriver : DisplayDriver<Coupon>
{
    public override Task<IDisplayResult> DisplayAsync(Coupon coupon, BuildDisplayContext context)
    {
        // A coupon is only displayed as a row of the admin list.
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return Task.FromResult<IDisplayResult>(null);
        }

        // The row's classes and attributes are rendered on its row element, in both admin list layouts.
        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = coupon.Code?.ToLowerInvariant() ?? string.Empty;
        }

        return CombineAsync(
            View("Coupon_Fields_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Coupon_Description_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5")
                .RenderWhen(() => Task.FromResult(!string.IsNullOrEmpty(coupon.Description) && !coupon.IsAutomatic)),
            View("Coupon_Status_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Coupon_Discount_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Discount:1"),
            View("Coupon_Usage_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:2")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Usage:1"),
            View("Coupon_Ends_SummaryAdmin", coupon)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Meta:3")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Ends:1"),
            View("Coupon_Buttons_SummaryAdmin", coupon).Location("Actions:5"));
    }
}
