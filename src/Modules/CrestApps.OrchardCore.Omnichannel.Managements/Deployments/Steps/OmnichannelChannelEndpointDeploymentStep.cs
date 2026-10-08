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

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDeploymentStep"/> class.
    /// </summary>
    public OmnichannelChannelEndpointDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.ChannelEndpoint;
        Category = _category;
        Title = _title;
    }
}
