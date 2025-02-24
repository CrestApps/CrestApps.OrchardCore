namespace CrestApps.OrchardCore.AI.Models;

public sealed class ValidatedModelDeploymentContext : AIDeploymentContextBase
{
    public readonly ValidationResultDetails Result;

    public ValidatedModelDeploymentContext(AIDeployment deployment, ValidationResultDetails result)
        : base(deployment)
    {
        Result = result ?? new();
    }
}
