namespace CrestApps.OrchardCore.AI.Models;

public sealed class DeletingContext<T> : HandlerContextBase<T>
{
    public DeletingContext(T model)
        : base(model)
    {
    }
}
