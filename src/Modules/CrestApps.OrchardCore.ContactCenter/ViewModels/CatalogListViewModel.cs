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
    /// Gets or sets the stable name of the screen's breadcrumb trail, e.g. <c>ContactCenterQueues</c>.
    /// </summary>
    public string BreadcrumbName { get; set; }

    /// <summary>
    /// Gets or sets the localized page title.
    /// </summary>
    public IHtmlContent Title { get; set; }

    /// <summary>
    /// Gets or sets the localized label for the create button.
    /// </summary>
    public IHtmlContent CreateLabel { get; set; }

    /// <summary>
    /// Gets or sets the kinds of entry that can be added, when there is more than one. The create button then opens a
    /// dialog with a card per kind, as the address list does; with one or none, the button adds an entry directly.
    /// </summary>
    public IList<CatalogCreateOption> CreateOptions { get; set; } = [];

    /// <summary>
    /// Gets or sets the localized title of the dialog that asks which kind of entry to add.
    /// </summary>
    public IHtmlContent CreateDialogTitle { get; set; }

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

/// <summary>
/// A kind of entry a catalog list can add.
/// </summary>
public class CatalogCreateOption
{
    /// <summary>
    /// Gets or sets the label shown for the kind.
    /// </summary>
    public string Label { get; set; }

    /// <summary>
    /// Gets or sets what the kind is for, shown on its card.
    /// </summary>
    public string Description { get; set; }

    /// <summary>
    /// Gets or sets the route values the create action is called with.
    /// </summary>
    public IDictionary<string, string> RouteValues { get; set; } = new Dictionary<string, string>();
}
