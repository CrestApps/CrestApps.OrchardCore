namespace CrestApps.OrchardCore.Models;

public sealed class DeletingContext<T> : HandlerContextBase<T>
{
    public DeletingContext(T model)
        : base(model)
    {
    }
}
