namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class UpdatedModelDeploymentContext : OpenAIDeploymentContextBase
{
    public UpdatedModelDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
