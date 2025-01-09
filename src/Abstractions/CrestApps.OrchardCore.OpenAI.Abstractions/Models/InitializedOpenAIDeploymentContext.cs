namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializedOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public InitializedOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
