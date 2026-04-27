namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for list source catalog entry.
/// </summary>
public class ListSourceCatalogEntryViewModel<T> : ListSourceModelViewModel
{
    /// <summary>
    /// Gets or sets the models.
    /// </summary>
    public IList<CatalogEntryViewModel<T>> Models { get; set; }
}
