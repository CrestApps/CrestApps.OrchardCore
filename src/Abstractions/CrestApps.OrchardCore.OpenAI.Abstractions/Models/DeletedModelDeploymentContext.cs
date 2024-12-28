namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class DeletedModelDeploymentContext : ModelDeploymentContextBase
{
    public DeletedModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}


