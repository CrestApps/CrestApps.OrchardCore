namespace CrestApps.OrchardCore.Core.Models;

public class ListCatalogEntryViewModel
{
    public CatalogEntryOptions Options { get; set; }

    public dynamic Pager { get; set; }
}

public class ListCatalogEntryViewModel<T> : ListCatalogEntryViewModel
{
    public IList<T> Models { get; set; }

    public IList<CatalogEntryViewModel<T>> CustomModels { get; set; }
}
