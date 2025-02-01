using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.AI.Models;

public sealed class UpdatingModelDeploymentContext : AIDeploymentContextBase
{
    public JsonNode Data { get; }

    public UpdatingModelDeploymentContext(AIDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
