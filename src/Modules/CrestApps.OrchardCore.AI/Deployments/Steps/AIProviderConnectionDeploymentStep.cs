using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

internal sealed class AIProviderConnectionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDeploymentStep"/> class.
    /// </summary>
    public AIProviderConnectionDeploymentStep()
    {
        Name = AIProviderConnectionsStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the connection ids.
    /// </summary>
    public string[] ConnectionIds { get; set; }
}
