using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class UpdatingAIToolInstanceContext : AIToolInstanceContextBase
{
    public JsonNode Data { get; }

    public UpdatingAIToolInstanceContext(AIToolInstance instance, JsonNode data)
        : base(instance)
    {
        Data = data ?? new JsonObject();
    }
}
