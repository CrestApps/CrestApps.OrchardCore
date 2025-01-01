namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class SavedOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public SavedOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
