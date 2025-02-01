using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.AI.Json;

namespace CrestApps.OrchardCore.AI.Models;

public class AIConnectionOptions
{
    public Dictionary<string, IList<AIConnectionEntry>> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[JsonConverter(typeof(AIConnectionEntryConverter))]
public sealed class AIConnectionEntry
{
    public string Name { get; set; }

    public JsonObject Data { get; set; } = new JsonObject(new JsonNodeOptions()
    {
        PropertyNameCaseInsensitive = true,
    });
}
