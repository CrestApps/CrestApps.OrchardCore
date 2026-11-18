using CrestApps.Core;
using CrestApps.Core.AI.Models;
using CrestApps.Core.AI.WebCrawlers.Strategies;
using CrestApps.Core.AI.WebCrawlers.Strategies.Sitemap;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.AI.Core;
using CrestApps.OrchardCore.AI.WebCrawlers.Services;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.AI.WebCrawlers.AI;

/// <summary>
/// What the web crawler tools share: the crawl strategies, finding a crawler as the Web Crawlers page does, its admin
/// links and the fields a tool returns.
/// </summary>
internal static class WebCrawlerToolSupport
{
    /// <summary>
    /// The area of the Web Crawlers admin pages: this module.
    /// </summary>
    public static readonly string Area = typeof(WebCrawlerToolSupport).Assembly.GetName().Name;

    /// <summary>
    /// Gets the crawl strategies enabled on this site.
    /// </summary>
    public static IReadOnlyList<WebCrawlerStrategyDescriptor> GetStrategies(IServiceProvider services)
        => services.GetService<IOptions<WebCrawlerStrategyOptions>>()?.Value?.Strategies
            .OrderBy(strategy => strategy.DisplayName.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

    /// <summary>
    /// Finds a crawler by its ID. A record of the same catalog that is not a crawl strategy is not a crawler, as on the
    /// Web Crawlers page.
    /// </summary>
    public static async Task<WebCrawler> FindAsync(IServiceProvider services, string id, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return null;
        }

        var crawler = await services.GetRequiredService<ISourceCatalogManager<WebCrawler>>().FindByIdAsync(id.Trim(), cancellationToken);

        return crawler is not null && WebCrawlerRecords.IsCrawlStrategy(crawler.Source, GetStrategies(services))
            ? crawler
            : null;
    }

    /// <summary>
    /// Gets the path of a crawler's editor.
    /// </summary>
    public static string GetEditPath(IServiceProvider services, string id)
        => AIModuleTools.GetAdminPath(services, Area, "WebCrawlers", "Edit", new RouteValueDictionary { ["id"] = id });

    /// <summary>
    /// Gets the path of the Web Crawlers page.
    /// </summary>
    public static string GetIndexPath(IServiceProvider services)
        => AIModuleTools.GetAdminPath(services, Area, "WebCrawlers", "Index");

    /// <summary>
    /// Describes a crawler from an explicit list of fields.
    /// </summary>
    public static async Task<object> DescribeAsync(IServiceProvider services, WebCrawler crawler, CancellationToken cancellationToken)
    {
        var dataSource = string.IsNullOrEmpty(crawler.AIDataSourceId)
            ? null
            : await services.GetRequiredService<ISourceCatalogManager<AIDataSource>>().FindByIdAsync(crawler.AIDataSourceId, cancellationToken);
        var localClock = services.GetService<ILocalClock>();
        var sitemap = crawler.TryGet<SitemapWebCrawlerMetadata>(out var metadata) ? metadata : null;

        return new
        {
            id = crawler.ItemId,
            displayText = crawler.DisplayText,
            strategy = crawler.Source,
            dataSource = new
            {
                id = crawler.AIDataSourceId,
                displayText = dataSource?.DisplayText,
                source = dataSource?.Source,
                found = dataSource is not null,
            },
            enabled = crawler.Enabled,
            reindexIntervalMinutes = crawler.ReindexIntervalMinutes,
            sitemap = sitemap is null
                ? null
                : new
                {
                    baseUrl = sitemap.BaseUrl,
                    sitemapUrl = sitemap.SitemapUrl,
                    maxPages = sitemap.MaxPages,
                    maxConcurrentRequests = sitemap.MaxConcurrentRequests,
                    requestTimeoutSeconds = sitemap.RequestTimeoutSeconds,
                    userAgent = sitemap.UserAgent,
                    includeUrlPatterns = sitemap.IncludeUrlPatterns ?? [],
                    excludeUrlPatterns = sitemap.ExcludeUrlPatterns ?? [],
                },
            createdAt = await AIModuleTools.ToLocalAsync(localClock, crawler.CreatedUtc),
            modifiedAt = await AIModuleTools.ToLocalAsync(localClock, crawler.ModifiedUtc),
            author = crawler.Author,
            editUrl = GetEditPath(services, crawler.ItemId),
        };
    }
}
