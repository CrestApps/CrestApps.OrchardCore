namespace CrestApps.OrchardCore.OpenAI.Models;

public sealed class ValidatingModelDeploymentContext : ModelDeploymentContextBase
{
    public ModelDeploymentValidateResult Result { get; } = new();

    public ValidatingModelDeploymentContext(ModelDeployment deployment)
        : base(deployment)
    {
    }
}
