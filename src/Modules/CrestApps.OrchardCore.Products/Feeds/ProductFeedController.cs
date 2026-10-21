using System.Text;
using CrestApps.OrchardCore.Products.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Products.Feeds;

/// <summary>
/// The feeds a shopping channel fetches: <c>feeds/products.xml</c> (Google Merchant Center, Meta, TikTok) and
/// <c>feeds/products.csv</c>, in the store's currency or <c>?currency=EUR</c>, with <c>?key=</c> when the feed has one.
/// </summary>
[Feature(ProductConstants.Feature.Feeds)]
public sealed class ProductFeedController : Controller
{
    private readonly ProductFeedService _feeds;
    private readonly ISiteService _siteService;

    public ProductFeedController(
        ProductFeedService feeds,
        ISiteService siteService)
    {
        _feeds = feeds;
        _siteService = siteService;
    }

    [HttpGet("feeds/products.xml")]
    public async Task<IActionResult> Xml(string currency, string key)
    {
        if (!await _feeds.IsAllowedAsync(key))
        {
            return NotFound();
        }

        var site = await _siteService.GetSiteSettingsAsync();
        var feed = await _feeds.GetAsync(Currency(currency), cancellationToken: HttpContext.RequestAborted);

        return Content(ProductFeedWriter.ToXml(feed, site.SiteName, site.BaseUrl ?? $"{Request.Scheme}://{Request.Host}{Request.PathBase}"), "application/xml; charset=utf-8");
    }

    [HttpGet("feeds/products.csv")]
    public async Task<IActionResult> Csv(string currency, string key)
    {
        if (!await _feeds.IsAllowedAsync(key))
        {
            return NotFound();
        }

        var feed = await _feeds.GetAsync(Currency(currency), cancellationToken: HttpContext.RequestAborted);

        return File(Encoding.UTF8.GetBytes(ProductFeedWriter.ToCsv(feed)), "text/csv; charset=utf-8", "products.csv");
    }

    private static string Currency(string currency)
        => CurrencyCodeUtility.IsValid(currency)
            ? CurrencyCodeUtility.Normalize(currency)
            : null;
}
