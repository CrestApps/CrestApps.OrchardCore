using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class UpdatingModelDeploymentContext : ModelDeploymentContextBase
{
    public JsonNode Data { get; }

    public UpdatingModelDeploymentContext(ModelDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
