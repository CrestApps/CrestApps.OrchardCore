namespace CrestApps.OrchardCore.AI.Exceptions;

public class UnregisteredCompletionClientException : Exception
{
    public UnregisteredCompletionClientException(string clientName)
        : base($"No registered completion client was found to match '${clientName}'.")
    {
    }
}
