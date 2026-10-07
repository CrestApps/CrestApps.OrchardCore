using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI deployment deletion instructions.
/// </summary>
public sealed class DeleteAIDeploymentDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<DeleteAIDeploymentDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<DeleteAIDeploymentDeploymentStep>("Delete AI Deployments");

    /// <summary>
    /// Initializes a new instance of the <see cref="DeleteAIDeploymentDeploymentStep"/> class.
    /// </summary>
    public DeleteAIDeploymentDeploymentStep()
    {
        Name = DeleteAIDeploymentStep.StepKey;
        Category = _category;
        Title = _title;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the deployment names.
    /// </summary>
    public string[] DeploymentNames { get; set; }
}
