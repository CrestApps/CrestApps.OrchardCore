namespace CrestApps.OrchardCore.AI.Models;

public sealed class DeletingAIDeploymentContext : AIDeploymentContextBase
{
    public DeletingAIDeploymentContext(AIDeployment deployment)
        : base(deployment)
    {
    }
}
