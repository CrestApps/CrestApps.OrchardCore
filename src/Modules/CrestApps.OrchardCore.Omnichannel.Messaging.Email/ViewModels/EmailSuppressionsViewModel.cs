using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.Omnichannel.Messaging.Email.ViewModels;

/// <summary>
/// The list of suppressed email addresses.
/// </summary>
public class EmailSuppressionsViewModel
{
    /// <summary>
    /// Gets or sets the search and the bulk actions of the list.
    /// </summary>
    public CatalogEntryOptions<EmailSuppressionBulkAction> Options { get; set; } = new();

    /// <summary>
    /// Gets or sets the total number of suppressed addresses matching the search.
    /// </summary>
    public int TotalCount { get; set; }

    /// <summary>
    /// Gets or sets the rows, the bulk actions and the pager the <c>admin-list</c> tag renders, in the layout
    /// (List or Grid) the site or the user picked.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}

/// <summary>
/// What can be done to the addresses selected in the suppression list.
/// </summary>
public enum EmailSuppressionBulkAction
{
    /// <summary>
    /// Nothing.
    /// </summary>
    None,

    /// <summary>
    /// Takes each selected address off the list, so it can be emailed again.
    /// </summary>
    Remove,
}
