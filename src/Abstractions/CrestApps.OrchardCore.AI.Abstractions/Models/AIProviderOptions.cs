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

    public string DefaultChatDeploymentName { get; set; }

    public string DefaultEmbeddingDeploymentName { get; set; }

    public string DefaultImagesDeploymentName { get; set; }

    public string DefaultUtilityDeploymentName { get; set; }

    public IDictionary<string, AIProviderConnectionEntry> Connections { get; set; }
}

[JsonConverter(typeof(AIProviderConnectionConverter))]
public sealed class AIProviderConnectionEntry : ReadOnlyDictionary<string, object>
{
    public AIProviderConnectionEntry(AIProviderConnectionEntry connection)
        : base(connection)
    {
    }

    public AIProviderConnectionEntry(IDictionary<string, object> dictionary)
        : base(dictionary)
    {
    }
}
