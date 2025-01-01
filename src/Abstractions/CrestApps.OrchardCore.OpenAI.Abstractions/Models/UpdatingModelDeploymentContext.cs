using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class UpdatingModelDeploymentContext : OpenAIDeploymentContextBase
{
    public JsonNode Data { get; }

    public UpdatingModelDeploymentContext(OpenAIDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
