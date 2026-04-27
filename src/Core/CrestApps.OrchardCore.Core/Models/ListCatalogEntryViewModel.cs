namespace CrestApps.OrchardCore.Core.Models;

/// <summary>
/// Represents the view model for list catalog entry.
/// </summary>
public class ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the options.
    /// </summary>
    public CatalogEntryOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape used to render pagination controls.
    /// Downstream consumers should cast to the concrete pager type.
    /// </summary>
    public object Pager { get; set; }
}

/// <summary>
/// Represents the view model for list catalog entry.
/// </summary>
public class ListCatalogEntryViewModel<T> : ListCatalogEntryViewModel
{
    /// <summary>
    /// Gets or sets the models.
    /// </summary>
    public IList<T> Models { get; set; }
}
