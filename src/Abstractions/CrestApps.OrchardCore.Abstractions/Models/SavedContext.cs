namespace CrestApps.OrchardCore.Models;

public sealed class SavedContext<T> : HandlerContextBase<T>
{
    public SavedContext(T model)
        : base(model)
    {
    }
}
