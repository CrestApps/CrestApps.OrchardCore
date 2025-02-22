namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedModelDeploymentContext : AIDeploymentContextBase
{
    public readonly AIValidateResult Result;

    public ValidatedModelDeploymentContext(AIDeployment deployment, AIValidateResult result)
        : base(deployment)
    {
        Result = result ?? new();
    }
}
