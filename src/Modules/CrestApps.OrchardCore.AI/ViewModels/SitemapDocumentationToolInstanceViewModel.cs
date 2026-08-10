namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the source specific fields captured for a sitemap crawling documentation search tool instance.
/// </summary>
public class SitemapDocumentationToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the base URL of the documentation site.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the explicit sitemap URL. When empty, it is derived from the base URL.
    /// </summary>
    public string SitemapUrl { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results returned for a single search.
    /// </summary>
    public int? MaxResults { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of pages the crawler indexes.
    /// </summary>
    public int? MaxPages { get; set; }
}
