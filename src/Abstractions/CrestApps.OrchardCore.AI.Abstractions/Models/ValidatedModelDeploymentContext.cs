namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedModelDeploymentContext : AIDeploymentContextBase
{
    public readonly AIDeploymentValidateResult Result;

    public ValidatedModelDeploymentContext(AIDeployment deployment, AIDeploymentValidateResult result)
        : base(deployment)
    {
        Result = result ?? new();
    }
}
