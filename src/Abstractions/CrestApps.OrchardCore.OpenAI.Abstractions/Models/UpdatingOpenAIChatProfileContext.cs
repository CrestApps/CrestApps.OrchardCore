using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class UpdatingOpenAIChatProfileContext : OpenAIChatProfileContextBase
{
    public JsonNode Data { get; }

    public UpdatingOpenAIChatProfileContext(OpenAIChatProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}
