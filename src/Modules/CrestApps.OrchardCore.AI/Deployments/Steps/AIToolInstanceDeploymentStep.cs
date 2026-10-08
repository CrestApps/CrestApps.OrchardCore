using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI tool instances.
/// </summary>
public sealed class AIToolInstanceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIToolInstanceDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIToolInstanceDeploymentStep>("AI Tool Instances");

    /// <summary>
    /// Initializes a new instance of the <see cref="AIToolInstanceDeploymentStep"/> class.
    /// </summary>
    public AIToolInstanceDeploymentStep()
    {
        Name = AIToolInstanceStep.StepKey;
        Category = _category;
        Title = _title;
    }

    /// <summary>
    /// Gets or sets a value indicating whether all tool instances are exported.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the identifiers of the tool instances to export.
    /// </summary>
    public string[] InstanceIds { get; set; }
}
