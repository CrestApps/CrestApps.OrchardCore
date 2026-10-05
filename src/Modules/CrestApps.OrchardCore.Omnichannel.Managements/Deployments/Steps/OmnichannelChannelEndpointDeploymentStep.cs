using OrchardCore.Deployment;
using OrchardCore.Localization;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;

/// <summary>
/// Represents a deployment step that exports Omnichannel channel endpoints.
/// </summary>
public sealed class OmnichannelChannelEndpointDeploymentStep : DeploymentStep
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDeploymentStep"/> class.
    /// </summary>
    public OmnichannelChannelEndpointDeploymentStep()
    {
        Name = OmnichannelDeploymentSteps.ChannelEndpoint;
        Category = LocalizationSource.Create<OmnichannelChannelEndpointDeploymentStep>("Omnichannel");
    }
}
