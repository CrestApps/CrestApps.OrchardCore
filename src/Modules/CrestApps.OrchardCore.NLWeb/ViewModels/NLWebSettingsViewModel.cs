using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CrestApps.OrchardCore.NLWeb.ViewModels;

/// <summary>
/// The editor of the NLWeb site settings.
/// </summary>
public class NLWebSettingsViewModel
{
    /// <summary>
    /// Gets or sets the names of the search indexes questions are answered from.
    /// </summary>
    public string[] IndexProfileNames { get; set; } = [];

    /// <summary>
    /// Gets or sets the content types the answers can include.
    /// </summary>
    public string[] ContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the most results an answer has.
    /// </summary>
    public int MaxResults { get; set; }

    /// <summary>
    /// Gets or sets the response mode used when the caller does not ask for one.
    /// </summary>
    public string DefaultMode { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the original query-string protocol is accepted.
    /// </summary>
    public bool AllowLegacyRequests { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether pages on other sites can call the endpoints from a browser.
    /// </summary>
    public bool AllowCrossOrigin { get; set; }

    /// <summary>
    /// Gets or sets the search indexes to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableIndexProfiles { get; set; } = [];

    /// <summary>
    /// Gets or sets the content types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> AvailableContentTypes { get; set; } = [];

    /// <summary>
    /// Gets or sets the response modes to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> Modes { get; set; } = [];

    /// <summary>
    /// Gets or sets a value indicating whether a search provider can search the indexes.
    /// </summary>
    [BindNever]
    public bool HasSearchProvider { get; set; }

    /// <summary>
    /// Gets or sets the absolute URL of the ask endpoint.
    /// </summary>
    [BindNever]
    public string AskUrl { get; set; }
}
