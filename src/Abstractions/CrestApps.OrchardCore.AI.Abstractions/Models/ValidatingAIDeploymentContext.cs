namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIDeploymentContext : AIDeploymentContextBase
{
    public ValidationResultDetails Result { get; }

    public ValidatingAIDeploymentContext(AIDeployment deployment)
        : base(deployment)
    {
    }
}
