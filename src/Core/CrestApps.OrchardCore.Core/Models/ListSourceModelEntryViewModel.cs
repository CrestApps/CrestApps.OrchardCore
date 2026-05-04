namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for list source model entry.
/// </summary>
public class ListSourceModelEntryViewModel<T, TName> : ListSourceModelViewModel<TName>
{
    /// <summary>
    /// Gets or sets the models.
    /// </summary>
    public IList<CatalogEntryViewModel<T>> Models { get; set; }
}
