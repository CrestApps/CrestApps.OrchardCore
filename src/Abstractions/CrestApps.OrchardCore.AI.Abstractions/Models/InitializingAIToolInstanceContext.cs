using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class InitializingAIToolInstanceContext : AIToolInstanceContextBase
{
    public JsonNode Data { get; }

    public InitializingAIToolInstanceContext(AIToolInstance instance, JsonNode data)
        : base(instance)
    {
        Data = data ?? new JsonObject();
    }
}
