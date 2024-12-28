namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatedModelDeploymentContext : ModelDeploymentContextBase
{
    public readonly ModelDeploymentValidateResult Result;

    public ValidatedModelDeploymentContext(ModelDeployment deployment, ModelDeploymentValidateResult result)
        : base(deployment)
    {
        Result = result ?? new();
    }
}
