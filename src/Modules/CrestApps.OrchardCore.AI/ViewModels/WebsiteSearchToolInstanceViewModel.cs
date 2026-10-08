namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the source specific fields captured for a live website search tool instance. The instance
/// queries a site's own search API on every request (no crawling or local corpus) and maps the JSON
/// response to documentation results. The defaults target the WordPress REST search endpoint.
/// </summary>
public class WebsiteSearchToolInstanceViewModel
{
    /// <summary>
    /// Gets or sets the base URL of the site (for example <c>https://www.example.com</c>).
    /// </summary>
    public string BaseUrl { get; set; }

    /// <summary>
    /// Gets or sets the search endpoint path appended to the base URL. Defaults to the WordPress REST
    /// search endpoint.
    /// </summary>
    public string SearchPath { get; set; } = "/wp-json/wp/v2/search";

    /// <summary>
    /// Gets or sets the query-string parameter that carries the model's free-text query.
    /// </summary>
    public string QueryParameter { get; set; } = "search";

    /// <summary>
    /// Gets or sets fixed extra query-string parameters always appended to the request.
    /// </summary>
    public string ExtraQuery { get; set; } = "_embed=1";

    /// <summary>
    /// Gets or sets the dotted path to the results array within the JSON response. Empty means the
    /// response body is itself the array.
    /// </summary>
    public string ResultsPath { get; set; }

    /// <summary>
    /// Gets or sets the dotted path, relative to each result element, to the result title.
    /// </summary>
    public string TitlePath { get; set; } = "title";

    /// <summary>
    /// Gets or sets the dotted path, relative to each result element, to the result URL.
    /// </summary>
    public string UrlPath { get; set; } = "url";

    /// <summary>
    /// Gets or sets the dotted path, relative to each result element, to the text snippet.
    /// </summary>
    public string SnippetPath { get; set; } = "_embedded.self[0].excerpt.rendered";

    /// <summary>
    /// Gets or sets the maximum number of results returned for a single search.
    /// </summary>
    public int? MaxResults { get; set; }
}
