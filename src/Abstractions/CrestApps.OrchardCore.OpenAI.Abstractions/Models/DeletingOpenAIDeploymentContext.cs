namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class DeletingOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public DeletingOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
