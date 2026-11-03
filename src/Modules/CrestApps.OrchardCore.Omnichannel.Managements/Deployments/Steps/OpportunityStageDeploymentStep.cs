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
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
