using CrestApps.OrchardCore.Core.Models;

namespace CrestApps.OrchardCore.AI.ViewModels;

/// <summary>
/// Represents the view model for list catalog entry with read only.
/// </summary>
public class ListCatalogEntryWithReadOnlyViewModel<T> : ListSourceCatalogEntryViewModel<T>
{
    /// <summary>
    /// Gets or sets the read only models.
    /// </summary>
    public IList<CatalogEntryViewModel<T>> ReadOnlyModels { get; set; } = [];
}
