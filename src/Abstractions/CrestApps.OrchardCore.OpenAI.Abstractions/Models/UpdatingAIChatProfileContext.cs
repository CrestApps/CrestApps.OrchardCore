using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class UpdatingAIChatProfileContext : AIChatProfileContextBase
{
    public JsonNode Data { get; }

    public UpdatingAIChatProfileContext(AIChatProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}