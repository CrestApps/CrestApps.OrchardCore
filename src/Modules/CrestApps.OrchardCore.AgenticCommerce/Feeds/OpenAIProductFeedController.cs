using CrestApps.OrchardCore.Products.Core;
using CrestApps.OrchardCore.Products.Core.Feeds;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AgenticCommerce.Feeds;

/// <summary>
/// The OpenAI product feed for ChatGPT: <c>feeds/openai/products.jsonl.gz</c> and <c>feeds/openai/products.csv.gz</c>,
/// or uncompressed without <c>.gz</c>, in each product's currency or <c>?currency=EUR</c>, with <c>?key=</c> when the
/// store's product feed has a key.
/// </summary>
[Feature(AgenticCommerceConstants.Features.Feeds)]
public sealed class OpenAIProductFeedController : Controller
{
    private readonly IProductFeedService _productFeeds;
    private readonly OpenAIProductFeedService _feeds;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIProductFeedController"/> class.
    /// </summary>
    /// <param name="productFeeds">The store's product feed, whose key protects this one too.</param>
    /// <param name="feeds">The OpenAI product feed.</param>
    public OpenAIProductFeedController(
        IProductFeedService productFeeds,
        OpenAIProductFeedService feeds)
    {
        _productFeeds = productFeeds;
        _feeds = feeds;
    }

    /// <summary>
    /// Returns the feed as gzipped JSON lines.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or none.</param>
    /// <param name="key">The feed's key, when it has one.</param>
    [HttpGet("feeds/openai/products.jsonl.gz")]
    public Task<IActionResult> JsonLinesGzip(string currency, string key)
        => FileAsync(OpenAIProductFeedFormats.JsonLinesGzip, currency, key);

    /// <summary>
    /// Returns the feed as gzipped CSV.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or none.</param>
    /// <param name="key">The feed's key, when it has one.</param>
    [HttpGet("feeds/openai/products.csv.gz")]
    public Task<IActionResult> CsvGzip(string currency, string key)
        => FileAsync(OpenAIProductFeedFormats.CsvGzip, currency, key);

    /// <summary>
    /// Returns the feed as JSON lines.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or none.</param>
    /// <param name="key">The feed's key, when it has one.</param>
    [HttpGet("feeds/openai/products.jsonl")]
    public Task<IActionResult> JsonLines(string currency, string key)
        => FileAsync(OpenAIProductFeedFormats.JsonLines, currency, key);

    /// <summary>
    /// Returns the feed as CSV.
    /// </summary>
    /// <param name="currency">The ISO 4217 code of the currency, or none.</param>
    /// <param name="key">The feed's key, when it has one.</param>
    [HttpGet("feeds/openai/products.csv")]
    public Task<IActionResult> Csv(string currency, string key)
        => FileAsync(OpenAIProductFeedFormats.Csv, currency, key);

    private async Task<IActionResult> FileAsync(string format, string currency, string key)
    {
        if (!await _productFeeds.IsAllowedAsync(key))
        {
            return NotFound();
        }

        var code = CurrencyCodeUtility.IsValid(currency) ? CurrencyCodeUtility.Normalize(currency) : null;
        var feed = await _feeds.GetAsync(code, cancellationToken: HttpContext.RequestAborted);
        var contentType = format switch
        {
            OpenAIProductFeedFormats.Csv => "text/csv; charset=utf-8",
            OpenAIProductFeedFormats.JsonLines => "application/jsonl; charset=utf-8",
            _ => "application/gzip",
        };

        return File(OpenAIProductFeedWriter.ToBytes(feed.Records, format), contentType, "products." + format);
    }
}
