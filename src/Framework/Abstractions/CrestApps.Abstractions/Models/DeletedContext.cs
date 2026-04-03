namespace CrestApps.Models;

public sealed class DeletedContext<T> : HandlerContextBase<T>
{
    public DeletedContext(T entry)
    : base(entry)
    {
    }
}
