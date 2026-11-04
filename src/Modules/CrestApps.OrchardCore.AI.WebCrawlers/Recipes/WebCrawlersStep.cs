using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Recipes;

/// <summary>
/// Adds or updates web crawlers from a <c>WebCrawlers</c> recipe step. A crawler is matched by its <c>ItemId</c>, then by
/// its name within the same crawl strategy, and its strategy must be registered on the site.
/// </summary>
/// <remarks>
/// <code>
/// { "name": "WebCrawlers", "Crawlers": [ { "ItemId": "...", "Source": "Sitemap", "DisplayText": "Help center", "AIDataSourceId": "...",
///   "Enabled": true, "ReindexIntervalMinutes": 1440, "Properties": { ... } } ] }
/// </code>
/// Secrets in a crawler's settings are never exported; a recipe that leaves them out keeps the stored ones.
/// </remarks>
internal sealed class WebCrawlersStep : IngestionSourceRecipeStep<WebCrawler>
{
    public const string StepKey = "WebCrawlers";

    public const string EntriesName = "Crawlers";

    private readonly WebCrawlerStrategyOptions _strategyOptions;

    internal readonly IStringLocalizer S;

    public WebCrawlersStep(
        ISourceCatalogManager<WebCrawler> manager,
        IOptions<WebCrawlerStrategyOptions> strategyOptions,
        IStringLocalizer<WebCrawlersStep> stringLocalizer)
        : base(StepKey, EntriesName, manager)
    {
        _strategyOptions = strategyOptions.Value;
        S = stringLocalizer;
    }

    protected override bool IsKnownSource(string source)
        => WebCrawlerRecords.IsCrawlStrategy(source, _strategyOptions.Strategies.ToArray());

    protected override string UnknownSourceError(string displayText, string source)
        => string.IsNullOrEmpty(source)
            ? S["The web crawler '{0}' does not name its crawl strategy in Source.", displayText]
            : S["The web crawler '{0}' uses the crawl strategy '{1}', which is not registered on this site.", displayText, source];
}
