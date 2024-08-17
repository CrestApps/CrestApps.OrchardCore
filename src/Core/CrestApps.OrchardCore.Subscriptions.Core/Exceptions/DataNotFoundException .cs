namespace CrestApps.OrchardCore.Subscriptions.Core.Exceptions;

public class DataNotFoundException : Exception
{
    public DataNotFoundException(string message)
        : base(message) { }
}
