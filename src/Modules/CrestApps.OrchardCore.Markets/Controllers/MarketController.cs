using CrestApps.OrchardCore.Markets.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Markets.Controllers;

/// <summary>
/// The storefront's market picker: keeps the market a shopper picks, and switches the storefront to its language.
/// </summary>
public sealed class MarketController : Controller
{
    private static readonly TimeSpan _cookieLifetime = TimeSpan.FromDays(365);

    private readonly ShopperMarketService _markets;
    private readonly ILocalizationService _localizationService;
    private readonly IClock _clock;

    public MarketController(
        ShopperMarketService markets,
        ILocalizationService localizationService,
        IClock clock)
    {
        _markets = markets;
        _localizationService = localizationService;
        _clock = clock;
    }

    [HttpPost("market")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Choose(string marketId, string returnUrl)
    {
        var market = ShopperMarketService.Find(await _markets.GetMarketsAsync(HttpContext.RequestAborted), marketId);
        var options = new CookieOptions
        {
            Expires = _clock.UtcNow.Add(_cookieLifetime),
            HttpOnly = true,
            IsEssential = true,
            Secure = Request.IsHttps,
            SameSite = SameSiteMode.Lax,
            Path = string.IsNullOrEmpty(Request.PathBase) ? "/" : Request.PathBase.Value,
        };

        // None picked is the store's own prices.
        if (market is null)
        {
            Response.Cookies.Delete(MarketsConstants.CookieName, options);
        }
        else
        {
            Response.Cookies.Append(MarketsConstants.CookieName, market.ItemId, options);

            if (!string.IsNullOrEmpty(market.Culture) &&
                (await _localizationService.GetSupportedCulturesAsync()).Contains(market.Culture, StringComparer.OrdinalIgnoreCase))
            {
                Response.Cookies.Append(CookieRequestCultureProvider.DefaultCookieName, CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(market.Culture)), options);
            }
        }

        return Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : Redirect("~/");
    }
}
