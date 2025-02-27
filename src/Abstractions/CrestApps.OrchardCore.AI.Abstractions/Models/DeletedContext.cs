namespace CrestApps.OrchardCore.AI.Models;

public sealed class DeletedContext<T> : HandlerContextBase<T>
{
    public DeletedContext(T model)
        : base(model)
    {
    }
}
