namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the source specific fields captured for an Algolia DocSearch documentation search tool instance.
/// </summary>
public class AlgoliaDocumentationToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the Algolia application identifier.
    /// </summary>
    public string ApplicationId { get; set; }

    /// <summary>
    /// Gets or sets the Algolia search-only API key.
    /// </summary>
    public string ApiKey { get; set; }

    /// <summary>
    /// Gets or sets the Algolia index name to query.
    /// </summary>
    public string IndexName { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of results returned for a single search.
    /// </summary>
    public int? MaxResults { get; set; }
}
