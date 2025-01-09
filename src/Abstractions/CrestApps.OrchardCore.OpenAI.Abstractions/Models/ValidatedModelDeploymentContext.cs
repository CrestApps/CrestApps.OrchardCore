namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatedModelDeploymentContext : OpenAIDeploymentContextBase
{
    public readonly OpenAIDeploymentValidateResult Result;

    public ValidatedModelDeploymentContext(OpenAIDeployment deployment, OpenAIDeploymentValidateResult result)
        : base(deployment)
    {
        Result = result ?? new();
    }
}
