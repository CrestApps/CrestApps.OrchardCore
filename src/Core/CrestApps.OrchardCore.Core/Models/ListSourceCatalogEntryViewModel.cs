namespace CrestApps.OrchardCore.Core.Models;

public class ListSourceModelViewModel : ListCatalogEntryViewModel
{
    public IEnumerable<string> Sources { get; set; }
}

public class ListSourceModelViewModel<TName> : ListCatalogEntryViewModel
{
    public IEnumerable<TName> Sources { get; set; }
}

public class ListSourceModelViewModel<T, TName> : ListCatalogEntryViewModel<TName>
{
    public IEnumerable<T> Sources { get; set; }
}

public class ListSourceCatalogEntryViewModel<T> : ListSourceModelViewModel
{
    public IList<CatalogEntryViewModel<T>> Models { get; set; }
}

public class ListSourceModelEntryViewModel<T, TName> : ListSourceModelViewModel<TName>
{
    public IList<CatalogEntryViewModel<T>> Models { get; set; }
}
