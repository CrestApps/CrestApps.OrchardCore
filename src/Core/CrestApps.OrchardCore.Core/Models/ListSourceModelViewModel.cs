namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for list source model.
/// </summary>
public class ListSourceModelViewModel : ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the sources.
    /// </summary>
    public IEnumerable<string> Sources { get; set; }
}

/// <summary>
/// Represents the view model for list source model.
/// </summary>
public class ListSourceModelViewModel<TName> : ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the sources.
    /// </summary>
    public IEnumerable<TName> Sources { get; set; }
}

/// <summary>
/// Represents the view model for list source model.
/// </summary>
public class ListSourceModelViewModel<T, TName> : ListCatalogEntryViewModel<TName>
{
    /// <summary>
    /// Gets or sets the sources.
    /// </summary>
    public IEnumerable<T> Sources { get; set; }
}
