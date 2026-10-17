using CrestApps.OrchardCore.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Omnichannel.Managements.ViewModels;

/// <summary>
/// The list of phone numbers known not to be in service.
/// </summary>
public class NotInServiceNumbersIndexViewModel
{
    /// <summary>
    /// Gets or sets the search and the bulk actions of the list.
    /// </summary>
    public CatalogEntryOptions<NotInServiceNumberBulkAction> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the numbers on this page.
    /// </summary>
    public IList<NotInServiceNumberEntry> Entries { get; set; } = [];

    /// <summary>
    /// Gets or sets the total number of marked numbers matching the search.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the pager shape.
    /// </summary>
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the rows, the bulk actions and the pager the <c>admin-list</c> tag renders, in the layout
    /// (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// What can be done to the numbers selected in the list of numbers known not to be in service.
/// </summary>
public enum NotInServiceNumberBulkAction
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// Removes the not-in-service mark from each selected number, so campaigns can load and dial it again. Nothing is
    /// dialed by doing so.
    /// </summary>
    AllowDialing,
}
