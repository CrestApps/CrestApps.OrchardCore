namespace CrestApps.OrchardCore.AI.Models;

public class InitializingAIProviderConnectionContext
{
    public readonly Dictionary<string, object> Values = [];

    public readonly AIProviderConnection Connection;

    public InitializingAIProviderConnectionContext(AIProviderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Connection = connection;
    }
}
