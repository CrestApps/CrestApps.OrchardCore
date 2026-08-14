using CrestApps.OrchardCore.AI.Recipes;
using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProviderConnectionDeploymentStep"/> class.
    /// </summary>
    /// <param name="S">The string localizer.</param>
    public AIProviderConnectionDeploymentStep(IStringLocalizer<AIProviderConnectionDeploymentStep> S)
        : this()
    {
        Category = S["Artificial Intelligence"];
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
