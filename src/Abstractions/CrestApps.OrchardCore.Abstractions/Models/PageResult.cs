namespace CrestApps.OrchardCore.Models;

public class PageResult<T>
{
    public int Count { get; set; }

    public IEnumerable<T> Models { get; set; }
}
