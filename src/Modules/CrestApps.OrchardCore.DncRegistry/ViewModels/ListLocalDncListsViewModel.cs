using CrestApps.OrchardCore.DncRegistry.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;

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
    /// Gets or sets the collection of summary shapes for each DNC list entry.
    /// </summary>
    [BindNever]
    public IList<dynamic> Entries { get; set; }

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
}
