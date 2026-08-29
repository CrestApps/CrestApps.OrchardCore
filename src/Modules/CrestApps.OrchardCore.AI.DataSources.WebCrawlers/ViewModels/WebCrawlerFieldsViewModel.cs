using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.AI.DataSources.WebCrawlers.ViewModels;

/// <summary>
/// View model for the fields shared by every web-crawler strategy.
/// </summary>
public sealed class WebCrawlerFieldsViewModel
{
    /// <summary>
    /// Gets or sets the human-readable display name of the crawler.
    /// </summary>
    public string DisplayText { get; set; }

    /// <summary>
    /// Gets or sets the identifier of the target Web AI data source that receives the scraped pages.
    /// </summary>
    public string AIDataSourceId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether this crawler is active.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Gets or sets how often, in minutes, the background task re-crawls this site. Empty uses the default.
    /// </summary>
    public int? ReindexIntervalMinutes { get; set; }

    [BindNever]
    public IEnumerable<SelectListItem> DataSources { get; set; } = [];

    [BindNever]
    public bool HasDataSources { get; set; }
}
