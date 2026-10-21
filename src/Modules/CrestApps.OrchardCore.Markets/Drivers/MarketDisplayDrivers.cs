using CrestApps.OrchardCore.Markets.Models;
using CrestApps.OrchardCore.Markets.Services;
using CrestApps.OrchardCore.Markets.ViewModels;
using Microsoft.AspNetCore.Http;
using OrchardCore;
using OrchardCore.Admin;
using OrchardCore.Admin.Models;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.DisplayManagement.Shapes;
using OrchardCore.DisplayManagement.Views;

namespace CrestApps.OrchardCore.Markets.Drivers;

/// <summary>
/// Renders a market as a row of the markets admin list, in both of its layouts (List and Grid).
/// </summary>
internal sealed class MarketDisplayDriver : DisplayDriver<Market>
{
    public override IDisplayResult Display(Market market, BuildDisplayContext context)
    {
        if (context.DisplayType is not (OrchardCoreConstants.DisplayType.SummaryAdmin or OrchardCoreConstants.DisplayType.SummaryAdminGrid))
        {
            return null;
        }

        if (context.Shape is Shape rowShape)
        {
            rowShape.Classes.Add("item");
            rowShape.Attributes["data-filter-value"] = market.Name?.ToLowerInvariant() ?? string.Empty;
        }

        return Combine(
            View("Market_Fields_SummaryAdmin", market)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Content:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:1"),
            View("Market_Description_SummaryAdmin", market)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Description:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Name:5"),
            View("Market_Status_SummaryAdmin", market)
                .Location(OrchardCoreConstants.DisplayType.SummaryAdmin, "Tags:1")
                .Location(OrchardCoreConstants.DisplayType.SummaryAdminGrid, "Status:1"),
            View("Market_Buttons_SummaryAdmin", market).Location("Actions:5"));
    }
}

/// <summary>
/// Puts the market picker in the storefront's navigation bar, before the cart link, when there are markets to pick
/// from. A theme restyles it by overriding the <c>MarketPicker</c> shape, or hides it with an empty template.
/// </summary>
internal sealed class MarketNavbarDisplayDriver : DisplayDriver<Navbar>
{
    private readonly ShopperMarketService _markets;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public MarketNavbarDisplayDriver(
        ShopperMarketService markets,
        IHttpContextAccessor httpContextAccessor)
    {
        _markets = markets;
        _httpContextAccessor = httpContextAccessor;
    }

    public override async Task<IDisplayResult> DisplayAsync(Navbar model, BuildDisplayContext context)
    {
        var httpContext = _httpContextAccessor.HttpContext;

        if (httpContext is null || AdminAttribute.IsApplied(httpContext))
        {
            return null;
        }

        var markets = await _markets.GetMarketsAsync(httpContext.RequestAborted);

        if (markets.Count == 0)
        {
            return null;
        }

        var current = await _markets.GetCurrentAsync(httpContext.RequestAborted);
        var storeCurrency = await _markets.GetStoreCurrencyAsync(httpContext.RequestAborted);

        return Initialize<MarketPickerViewModel>("MarketPicker", picker =>
        {
            picker.Markets = markets;
            picker.Current = current;
            picker.StoreCurrency = storeCurrency;
            picker.ReturnUrl = httpContext.Request.PathBase + httpContext.Request.Path + httpContext.Request.QueryString;
        }).Location("Detail", "Content:5");
    }
}
