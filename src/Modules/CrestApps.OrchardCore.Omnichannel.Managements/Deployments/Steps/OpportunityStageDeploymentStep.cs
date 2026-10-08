using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the opportunity stages.
/// </summary>
public sealed class OpportunityStageDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OpportunityStageDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OpportunityStageDeploymentStep>("Omnichannel Opportunity Stages");

    /// <summary>
    /// Initializes a new instance of the <see cref="OpportunityStageDeploymentStep"/> class.
    /// </summary>
    public OpportunityStageDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.OpportunityStage;
        Category = _category;
        Title = _title;
    }
}
