namespace CrestApps.OrchardCore.AI.Models;

public sealed class SavingContext<T> : HandlerContextBase<T>
{
    public SavingContext(T model)
        : base(model)
    {
    }
}
