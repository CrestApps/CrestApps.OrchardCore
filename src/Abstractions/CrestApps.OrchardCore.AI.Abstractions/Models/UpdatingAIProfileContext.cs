using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class UpdatingAIProfileContext : AIProfileContextBase
{
    public JsonNode Data { get; }

    public UpdatingAIProfileContext(AIProfile profile, JsonNode data)
        : base(profile)
    {
        Data = data ?? new JsonObject();
    }
}
