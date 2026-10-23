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

    /// <inheritdoc />
    public override string Name => OmnichannelDeploymentSteps.OpportunityStage;

    /// <inheritdoc />
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    public override LocalizationSource Title => _title;
}
