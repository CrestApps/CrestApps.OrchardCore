namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatingOpenAIDeploymentContext : OpenAIDeploymentContextBase
{
    public OpenAIDeploymentValidateResult Result { get; } = new();

    public ValidatingOpenAIDeploymentContext(OpenAIDeployment deployment)
        : base(deployment)
    {
    }
}
