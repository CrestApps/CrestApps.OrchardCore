using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Html;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.ContactCenter.ViewModels;

/// <summary>
/// View model for the shared <c>_CatalogList</c> partial that renders the near-identical Contact Center
/// catalog list screens (queues, skills, entry points, dialer profiles, queue groups, agent state reason
/// codes, business-hours calendars, and voice media). Per-type screens supply only the labels, the list name, the
/// columns particular to the catalog, and the admin list context; the markup lives in one place.
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
    /// Gets or sets the identifier the elements of the screen are named after, e.g. the dialog that asks which kind of
    /// entry to add.
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
    /// Gets or sets the stable name of the admin list, e.g. <c>ContactCenterQueues</c>. It keys the layout the user
    /// picked for the list, its column providers and its <c>AdminList-{Name}</c> alternates.
    /// </summary>
    public string ListName { get; set; }

    /// <summary>
    /// Gets or sets the entries and the pager the <c>admin-list</c> tag renders, in the layout (List or Grid) the site
    /// or the user picked.
    /// </summary>
    public AdminListContext List { get; set; }

    /// <summary>
    /// Gets or sets the columns of the Grid layout that are particular to the catalog. Every catalog list has a name,
    /// a last modified and an actions column; these sit between the name and the last modified column, in order.
    /// </summary>
    public IList<CatalogListColumn> Columns { get; set; } = [];
}

/// <summary>
/// A column of a catalog list in the Grid layout. It renders the row zone of the same name.
/// </summary>
public class CatalogListColumn
{
    /// <summary>
    /// Gets or sets the name of the column and of the row zone it renders, e.g. <c>Routing</c>.
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// Gets or sets the localized header of the column.
    /// </summary>
    public IHtmlContent Title { get; set; }
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
