using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core.Recipes;
using CrestApps.OrchardCore.AI.WebCrawlers.Recipes;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.AI.WebCrawlers.Deployments;

/// <summary>
/// Writes every web crawler into a <c>WebCrawlers</c> recipe step, without secrets. Records left in the crawler store
/// that are not crawlers, such as file sources not yet moved to their own store, are left out.
/// </summary>
internal sealed class WebCrawlersDeploymentSource : IngestionSourceDeploymentSource<WebCrawlersDeploymentStep, WebCrawler>
{
    private readonly WebCrawlerStrategyOptions _strategyOptions;

    public WebCrawlersDeploymentSource(
        ISourceCatalogManager<WebCrawler> manager,
        IOptions<WebCrawlerStrategyOptions> strategyOptions)
        : base(WebCrawlersStep.EntriesName, manager)
    {
        _strategyOptions = strategyOptions.Value;
    }

    protected override bool IsSelected(WebCrawlersDeploymentStep step, WebCrawler entry)
        => WebCrawlerRecords.IsCrawlStrategy(entry.Source, _strategyOptions.Strategies.ToArray());
}
