namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the source specific fields captured for a prebuilt search index documentation search tool instance.
/// </summary>
public class SearchIndexDocumentationToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the base URL of the documentation site.
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the explicit search index URL. When empty, it is derived from the base URL.
    /// </summary>
    public string IndexUrl { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results returned for a single search.
    /// </summary>
    public int? MaxResults { get; set; }
}
