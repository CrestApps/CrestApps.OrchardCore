using Microsoft.Extensions.Localization;
using OrchardCore.Deployment;

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
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDeploymentStep"/> class.
    /// </summary>
    /// <param name="stringLocalizer">The string localizer.</param>
    public OmnichannelChannelEndpointDeploymentStep(IStringLocalizer<OmnichannelChannelEndpointDeploymentStep> stringLocalizer)
        : this()
    {
        Category = stringLocalizer["Omnichannel"];
    }
}
