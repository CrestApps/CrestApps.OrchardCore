using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI provider connections.
/// </summary>
public sealed class AIProviderConnectionDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDeploymentStep"/> class.
    /// </summary>
    public AIProviderConnectionDeploymentStep()
    {
        Name = AIProviderConnectionsStep.StepKey;
        Category = LocalizationSource.Create<AIProviderConnectionDeploymentStep>("Artificial Intelligence");
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
