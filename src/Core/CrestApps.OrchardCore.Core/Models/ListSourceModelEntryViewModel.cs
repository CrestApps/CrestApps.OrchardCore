namespace CrestApps.OrchardCore.Core.Models;

public class ListSourceModelViewModel : ListModelViewModel
{
    public IEnumerable<string> Sources { get; set; }
}

public class ListSourceModelViewModel<TName> : ListModelViewModel
{
    public IEnumerable<TName> Sources { get; set; }
}

public class ListSourceModelViewModel<T, TName> : ListModelViewModel<TName>
{
    public IEnumerable<T> Sources { get; set; }
}

public class ListSourceModelEntryViewModel<T> : ListSourceModelViewModel
{
    public IList<ModelEntry<T>> Models { get; set; }
}

public class ListSourceModelEntryViewModel<T, TName> : ListSourceModelViewModel<TName>
{
    public IList<ModelEntry<T>> Models { get; set; }
}
