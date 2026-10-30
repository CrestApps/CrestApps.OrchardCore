using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel re-engagement cadences.
/// </summary>
public sealed class CadenceDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<CadenceDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<CadenceDeploymentStep>("Omnichannel Cadences");

    /// <inheritdoc />
    public override string Name => OmnichannelDeploymentSteps.Cadence;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
