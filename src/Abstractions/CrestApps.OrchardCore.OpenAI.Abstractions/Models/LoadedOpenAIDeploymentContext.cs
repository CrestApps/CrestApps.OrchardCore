namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class LoadedOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public LoadedOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
