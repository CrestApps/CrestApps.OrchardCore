using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports the lead statuses.
/// </summary>
public sealed class LeadStatusDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<LeadStatusDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<LeadStatusDeploymentStep>("Omnichannel Lead Statuses");

    /// <inheritdoc />
    public override string Name => OmnichannelDeploymentSteps.LeadStatus;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
