using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CrestApps.OrchardCore.OpenAI.Json;

namespace CrestApps.OrchardCore.OpenAI.Models;

public class OpenAIConnectionOptions
{
    public Dictionary<string, IList<OpenAIConnectionEntry>> Connections { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

[JsonConverter(typeof(OpenAIConnectionEntryConverter))]
public sealed class OpenAIConnectionEntry
{
    public string Name { get; set; }

    public JsonObject Data { get; set; } = new JsonObject(new JsonNodeOptions()
    {
        PropertyNameCaseInsensitive = true,
    });
}
