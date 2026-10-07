using CrestApps.OrchardCore.AI.Recipes;
using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.AI.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports AI profile templates.
/// </summary>
public sealed class AIProfileTemplateDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<AIProfileTemplateDeploymentStep>("Artificial Intelligence");
    private static readonly LocalizationSource _title = LocalizationSource.Create<AIProfileTemplateDeploymentStep>("AI Profile Templates");

    /// <summary>
    /// Initializes a new instance of the <see cref="AIProfileTemplateDeploymentStep"/> class.
    /// </summary>
    public AIProfileTemplateDeploymentStep()
    {
        Name = AIProfileTemplateStep.StepKey;
        Category = _category;
        Title = _title;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the template names.
    /// </summary>
    public string[] TemplateNames { get; set; }
}
