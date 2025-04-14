namespace CrestApps.OrchardCore.Core.Models;

public class ListModelViewModel
{
    public ModelOptions Options { get; set; }

    public dynamic Pager { get; set; }
}

public class ListModelViewModel<T> : ListModelViewModel
{
    public IList<T> Models { get; set; }
}
