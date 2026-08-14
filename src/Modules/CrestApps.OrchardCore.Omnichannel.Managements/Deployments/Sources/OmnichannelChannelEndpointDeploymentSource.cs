using System.Text.Json.Nodes;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

internal sealed class OmnichannelChannelEndpointDeploymentSource : DeploymentSourceBase<OmnichannelChannelEndpointDeploymentStep>
{
    private readonly IOmnichannelChannelEndpointManager _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelChannelEndpointDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the channel endpoints.</param>
    public OmnichannelChannelEndpointDeploymentSource(IOmnichannelChannelEndpointManager manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(OmnichannelChannelEndpointDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(OmnichannelDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = OmnichannelDeploymentSteps.ChannelEndpoint,
            ["ChannelEndpoints"] = data,
        });
    }
}
