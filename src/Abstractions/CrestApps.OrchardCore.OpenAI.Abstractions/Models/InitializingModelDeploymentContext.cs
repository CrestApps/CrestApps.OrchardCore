using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializingModelDeploymentContext : ModelDeploymentContextBase
{
    public JsonNode Data { get; }

    public InitializingModelDeploymentContext(ModelDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
