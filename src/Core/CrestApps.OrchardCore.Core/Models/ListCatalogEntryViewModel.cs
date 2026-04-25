namespace CrestApps.OrchardCore.Core.Models;

public class ListCatalogEntryViewModel
{
    public CatalogEntryOptions Options { get; set; }

    /// <summary>
    /// Gets or sets the pager shape used to render pagination controls.
    /// Downstream consumers should cast to the concrete pager type.
    /// </summary>
    public object Pager { get; set; }
}

public class ListCatalogEntryViewModel<T> : ListCatalogEntryViewModel
{
    public IList<T> Models { get; set; }
}
