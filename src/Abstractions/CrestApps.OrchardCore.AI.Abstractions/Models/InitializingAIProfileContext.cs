using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class InitializingAIProfileContext : AIProfileContextBase
{
    public JsonNode Data { get; }

    public InitializingAIProfileContext(AIProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}
