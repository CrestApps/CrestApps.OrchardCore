using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel channel endpoints.
/// </summary>
public sealed class OmnichannelChannelEndpointDeploymentStep : DeploymentStep
{
    private static readonly LocalizationSource _category = LocalizationSource.Create<OmnichannelChannelEndpointDeploymentStep>("Omnichannel");
    private static readonly LocalizationSource _title = LocalizationSource.Create<OmnichannelChannelEndpointDeploymentStep>("Omnichannel Addresses");

    /// <inheritdoc />
    public override string Name => OmnichannelDeploymentSteps.ChannelEndpoint;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Category => _category;

    /// <inheritdoc />
    [System.Text.Json.Serialization.JsonIgnore]
    public override LocalizationSource Title => _title;
}
