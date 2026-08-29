namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.ViewModels;

/// <summary>
/// View model for the sitemap crawl-strategy settings. Include/exclude patterns are edited as one
/// newline-separated value per box and are stored as a list of regular-expression patterns.
/// </summary>
public sealed class SitemapWebCrawlerViewModel
{
    /// <summary>
    /// Gets or sets the base URL of the site to scrape.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets an explicit sitemap or sitemap-index URL.
    /// </summary>
    public string SitemapUrl { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of pages to scrape.
    /// </summary>
    public int? MaxPages { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of concurrent page requests.
    /// </summary>
    public int? MaxConcurrentRequests { get; set; }

    /// <summary>
    /// Gets or sets the per-request fetch timeout, in seconds.
    /// </summary>
    public int? RequestTimeoutSeconds { get; set; }

    /// <summary>
    /// Gets or sets the User-Agent header presented while crawling.
    /// </summary>
    public string UserAgent { get; set; }

    /// <summary>
    /// Gets or sets the newline-separated include URL regular-expression patterns.
    /// </summary>
    public string IncludeUrlPatterns { get; set; }

    /// <summary>
    /// Gets or sets the newline-separated exclude URL regular-expression patterns.
    /// </summary>
    public string ExcludeUrlPatterns { get; set; }
}
