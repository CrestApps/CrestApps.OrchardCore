using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializingAIChatProfileContext : AIChatProfileContextBase
{
    public JsonNode Data { get; }

    public InitializingAIChatProfileContext(AIChatProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}
