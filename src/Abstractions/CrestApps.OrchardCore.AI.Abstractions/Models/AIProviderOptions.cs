using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Json;

namespace CrestApps.OrchardCore.AI.Models;

public class AIProviderOptions
{
    public Dictionary<string, AIProvider> Providers { get; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class AIProvider
{
    public string DefaultConnectionName { get; set; }

    public string DefaultDeploymentName { get; set; }

    public IDictionary<string, AIProviderConnection> Connections { get; set; }
}

[JsonConverter(typeof(AIProviderConnectionConverter))]
public sealed class AIProviderConnection : ReadOnlyDictionary<string, object>
{
    public AIProviderConnection(AIProviderConnection connection)
        : base(connection)
    {
    }

    public AIProviderConnection(IDictionary<string, object> dictionary)
        : base(dictionary)
    {
    }
}
