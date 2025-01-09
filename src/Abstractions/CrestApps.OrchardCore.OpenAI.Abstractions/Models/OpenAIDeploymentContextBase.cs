namespace CrestApps.OrchardCore.OpenAI.Models;

public abstract class OpenAIDeploymentContextBase
{
    public OpenAIDeployment Deployment { get; }

    public OpenAIDeploymentContextBase(OpenAIDeployment deployment)
    {
        ArgumentNullException.ThrowIfNull(deployment);

        Deployment = deployment;
    }
}
