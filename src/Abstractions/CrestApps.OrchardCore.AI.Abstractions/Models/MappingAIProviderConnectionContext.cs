namespace CrestApps.OrchardCore.AI.Models;

public class MappingAIProviderConnectionContext
{
    public readonly Dictionary<string, object> Values = [];

    public readonly AIProviderConnection Connection;

    public MappingAIProviderConnectionContext(AIProviderConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Connection = connection;
    }
}
