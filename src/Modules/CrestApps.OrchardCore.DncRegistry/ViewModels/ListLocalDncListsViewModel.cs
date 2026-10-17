using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.DncRegistry.ViewModels;

/// <summary>
/// View model for the local DNC registry list shape.
/// </summary>
public class ListLocalDncListsViewModel
{
    /// <summary>
    /// Gets or sets the filter/display options for the list page.
    /// </summary>
    public LocalDncListOptions Options { get; set; }


    /// <summary>
    /// Gets or sets the header shape containing action bar and summary.
    /// </summary>
    [BindNever]
    public dynamic Header { get; set; }

    /// <summary>
    /// Gets or sets the pager shape for navigation.
    /// </summary>
    [BindNever]
    public dynamic Pager { get; set; }

    /// <summary>
    /// Gets or sets the lists, the header and the pager the <c>admin-list</c> tag renders.
    /// </summary>
    [BindNever]
    public AdminListContext List { get; set; }
}
