using CrestApps.OrchardCore.AI.DataSources.Recipes;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.AI.DataSources.Deployments;

internal sealed class AIDataSourceDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIDataSourceDeploymentStep"/> class.
    /// </summary>
    public AIDataSourceDeploymentStep()
    {
        Name = AIDataSourceStep.StepKey;
    }

    /// <summary>
    /// Gets or sets the include all.
    /// </summary>
    public bool IncludeAll { get; set; }

    /// <summary>
    /// Gets or sets the source ids.
    /// </summary>
    public string[] SourceIds { get; set; }
}
