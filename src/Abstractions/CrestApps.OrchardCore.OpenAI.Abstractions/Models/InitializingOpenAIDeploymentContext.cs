using System.Text.Json.Nodes;

namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializingOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public JsonNode Data { get; }

    public InitializingOpenAIDeploymentContext(OpenAIDeployment deployment, JsonNode data)
        : base(deployment)
    {
        Data = data ?? new JsonObject();
    }
}
