namespace CrestApps.OrchardCore.Models;

public class PageResult<T>
{
    public int Count { get; set; }

    public IReadOnlyCollection<T> Entries { get; set; }
}
