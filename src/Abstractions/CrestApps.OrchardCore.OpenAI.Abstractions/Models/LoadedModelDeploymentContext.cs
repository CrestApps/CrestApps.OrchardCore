namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class LoadedModelDeploymentContext : ModelDeploymentContextBase
{
    public LoadedModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}
