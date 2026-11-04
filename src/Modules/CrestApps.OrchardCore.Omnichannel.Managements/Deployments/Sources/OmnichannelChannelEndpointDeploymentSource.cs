using CrestApps.OrchardCore.Core.Deployments;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Recipes.Models;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

/// <summary>
/// Exports every entry of the catalog as the <c>ChannelEndpoint</c> recipe step.
/// </summary>
internal sealed class OmnichannelChannelEndpointDeploymentSource : CatalogDeploymentSource<OmnichannelChannelEndpointDeploymentStep, OmnichannelChannelEndpoint>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the channel endpoints.</param>
    public OmnichannelChannelEndpointDeploymentSource(IOmnichannelChannelEndpointManager manager)
        : base("ChannelEndpoints", manager)
    {
    }
}
