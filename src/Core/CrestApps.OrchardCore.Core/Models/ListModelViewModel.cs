namespace CrestApps.OrchardCore.Core.Models;

public class ListModelViewModel
{
    public ModelOptions Options { get; set; }

    public IEnumerable<string> SourceNames { get; set; }

    public dynamic Pager { get; set; }
}

public class ListModelViewModel<T> : ListModelViewModel
{
    public IList<ModelEntry<T>> Models { get; set; }
}
