namespace CrestApps.OrchardCore.Models;

public class PageResult<T>
{
    public int Count { get; set; }

    public IEnumerable<T> Entries { get; set; }
}
