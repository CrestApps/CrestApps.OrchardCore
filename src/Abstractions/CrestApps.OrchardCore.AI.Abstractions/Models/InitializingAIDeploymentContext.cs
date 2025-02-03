using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class InitializingAIDeploymentContext : AIDeploymentContextBase
{
    public JsonNode Data { get; }

    public InitializingAIDeploymentContext(AIDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
