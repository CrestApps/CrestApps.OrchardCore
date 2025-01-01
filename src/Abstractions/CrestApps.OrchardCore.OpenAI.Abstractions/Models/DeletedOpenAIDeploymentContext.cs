namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class DeletedOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public DeletedOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}


