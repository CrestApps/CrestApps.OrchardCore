using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the opportunity stages.
/// </summary>
public sealed class OpportunityStageDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageDeploymentStep"/> class.
    /// </summary>
    public OpportunityStageDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.OpportunityStage;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OpportunityStageDeploymentStep(IStringLocalizer<OpportunityStageDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
