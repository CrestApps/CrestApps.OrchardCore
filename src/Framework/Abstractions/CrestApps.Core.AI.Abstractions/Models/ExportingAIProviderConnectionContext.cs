using System.Text.Json.Nodes;

namespace CrestApps.Core.AI.Models;

public class ExportingAIProviderConnectionContext
{
    public readonly AIProviderConnection Connection;

    public readonly JsonObject ExportData;

    public ExportingAIProviderConnectionContext(AIProviderConnection connection, JsonObject exportData)
    {
        ArgumentNullException.ThrowIfNull(connection);

        Connection = connection;
        ExportData = exportData ?? [];
    }
}
