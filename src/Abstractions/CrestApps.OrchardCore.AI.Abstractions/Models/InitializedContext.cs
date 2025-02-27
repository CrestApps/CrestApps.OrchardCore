namespace CrestApps.OrchardCore.AI.Models;

public sealed class InitializedContext<T> : HandlerContextBase<T>
{
    public InitializedContext(T model)
        : base(model)
    {
    }
}
