namespace CrestApps.OrchardCore.OpenAI.Models;

public abstract class ModelDeploymentContextBase
{
    public ModelDeployment Deployment { get; }

    public ModelDeploymentContextBase(ModelDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        Deployment = deployment;
    }
}
