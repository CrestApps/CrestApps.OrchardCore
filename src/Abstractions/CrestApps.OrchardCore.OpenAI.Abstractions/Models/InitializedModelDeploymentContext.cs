namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class InitializedModelDeploymentContext : ModelDeploymentContextBase
{
    public InitializedModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}
