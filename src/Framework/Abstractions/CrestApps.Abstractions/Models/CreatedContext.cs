namespace CrestApps.Models;

public sealed class CreatedContext<T> : HandlerContextBase<T>
{
    public CreatedContext(T model)
    : base(model)
    {
    }
}
