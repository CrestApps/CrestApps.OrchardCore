using CrestApps.OrchardCore.Core.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OrchardCore.Admin;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for list catalog entry with read only.
/// </summary>
public class ListCatalogEntryWithReadOnlyViewModel<T> : ListSourceCatalogEntryViewModel<T>
{
    /// <summary>
    /// Gets or sets the read-only entries, e.g. the ones defined in configuration, the second <c>admin-list</c>
    /// of the page renders. It is <see langword="null"/> when there are none.
    /// </summary>
    [BindNever]
    public AdminListContext ReadOnlyList { get; set; }
}
