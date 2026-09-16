using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Html;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for the shared <c>_CatalogList</c> partial that renders the near-identical Contact Center
/// catalog list screens (queues, skills, entry points, dialer profiles, queue groups, agent state reason
/// codes, and business-hours calendars). Per-type screens supply only the labels, list identifier, and the
/// mapped entries; the markup lives in one place.
/// </summary>
public class CatalogListViewModel
{
    /// <summary>
    /// Gets or sets the localized page title.
    /// </summary>
    public IHtmlContent Title { get; set; }

    /// <summary>
    /// Gets or sets the localized label for the create button.
    /// </summary>
    public IHtmlContent CreateLabel { get; set; }

    /// <summary>
    /// Gets or sets the identifier applied to the list element.
    /// </summary>
    public string ListId { get; set; }

    /// <summary>
    /// Gets or sets the localized message shown when the list is empty.
    /// </summary>
    public IHtmlContent EmptyMessage { get; set; }

    /// <summary>
    /// Gets or sets the catalog entry options that back the search field binding.
    /// </summary>
    public CatalogEntryOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the entries to render.
    /// </summary>
    public IList<CatalogListEntry> Entries { get; set; }

    /// <summary>
    /// Gets or sets the pager shape rendered beneath the list.
    /// </summary>
    public object Pager { get; set; }
}
