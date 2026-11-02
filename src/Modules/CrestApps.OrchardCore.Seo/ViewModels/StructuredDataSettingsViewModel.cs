using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CrestApps.OrchardCore.Seo.ViewModels;

/// <summary>
/// The editor of the structured data site settings.
/// </summary>
public class StructuredDataSettingsViewModel
{
    /// <summary>
    /// Gets or sets a value indicating whether the site's pages get a JSON-LD script.
    /// </summary>
    public bool RenderOnPages { get; set; }

    /// <summary>
    /// Gets or sets the schema.org type of the site's publisher, or empty for none.
    /// </summary>
    public string PublisherType { get; set; }

    /// <summary>
    /// Gets or sets the name of the publisher.
    /// </summary>
    public string PublisherName { get; set; }

    /// <summary>
    /// Gets or sets the URL of the publisher's logo.
    /// </summary>
    public string PublisherLogoUrl { get; set; }

    /// <summary>
    /// Gets or sets the publisher's profiles on other sites, one URL per line.
    /// </summary>
    public string SameAs { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the pages describe the site as a <c>WebSite</c>.
    /// </summary>
    public bool IncludeWebSite { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the <c>WebSite</c> tells search engines how to search the site.
    /// </summary>
    public bool IncludeSearchAction { get; set; }

    /// <summary>
    /// Gets or sets the publisher types to choose from.
    /// </summary>
    [BindNever]
    public IList<SelectListItem> PublisherTypes { get; set; } = [];
}
