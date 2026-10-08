using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI profiles.
/// </summary>
public sealed class AIProfileDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIProfileDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIProfileDeploymentStep>("AI Profiles");

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileDeploymentStep"/> class.
    /// </summary>
    public AIProfileDeploymentStep()
    {
        Name = AIProfileStep.StepKey;
        Category = _category;
        Title = _title;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the profile names.
    /// </summary>
    public string[] ProfileNames { get; set; }
}
