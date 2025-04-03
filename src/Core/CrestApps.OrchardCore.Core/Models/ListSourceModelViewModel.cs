namespace CrestApps.OrchardCore.Core.Models;

public class ListSourceModelViewModel : ListModelViewModel
{
    public IEnumerable<string> SourceNames { get; set; }
}

public class ListSourceModelViewModel<T> : ListSourceModelViewModel
{
    public IList<ModelEntry<T>> Models { get; set; }
}
