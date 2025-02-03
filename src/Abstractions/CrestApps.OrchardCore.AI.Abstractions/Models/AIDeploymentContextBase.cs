namespace CrestApps.OrchardCore.AI.Models;

public abstract class AIDeploymentContextBase
{
    public AIDeployment Deployment { get; }

    public AIDeploymentContextBase(AIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        Deployment = deployment;
    }
}
