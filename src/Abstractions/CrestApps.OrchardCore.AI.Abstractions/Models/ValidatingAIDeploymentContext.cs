namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatingAIDeploymentContext : AIDeploymentContextBase
{
    public AIValidateResult Result { get; } = new();

    public ValidatingAIDeploymentContext(AIDeployment deployment)
        : base(deployment)
    {
    }
}
