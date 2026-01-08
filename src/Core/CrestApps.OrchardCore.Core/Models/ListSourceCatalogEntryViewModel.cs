namespace CrestApps.OrchardCore.Core.Models;

public class ListSourceCatalogEntryViewModel<T> : ListSourceModelViewModel
{
    public IList<CatalogEntryViewModel<T>> Models { get; set; }
}
