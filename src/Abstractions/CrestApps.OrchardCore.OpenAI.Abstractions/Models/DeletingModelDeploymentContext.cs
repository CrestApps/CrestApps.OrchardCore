namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class DeletingModelDeploymentContext : ModelDeploymentContextBase
{
    public DeletingModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}
