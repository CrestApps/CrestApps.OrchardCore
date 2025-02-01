namespace CrestApps.OrchardCore.AI.Models;

public sealed class DeletedAIDeploymentContext : AIDeploymentContextBase
{
    public DeletedAIDeploymentContext(AIDeployment deployment)
        : base(deployment)
    {
    }
}
