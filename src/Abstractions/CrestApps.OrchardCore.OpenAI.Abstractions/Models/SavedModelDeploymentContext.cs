namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class SavedModelDeploymentContext : ModelDeploymentContextBase
{
    public SavedModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}
