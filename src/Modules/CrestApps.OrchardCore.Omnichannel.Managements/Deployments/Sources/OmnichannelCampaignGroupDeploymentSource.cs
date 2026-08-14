using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

internal sealed class OmnichannelCampaignGroupDeploymentSource : DeploymentSourceBase<OmnichannelCampaignGroupDeploymentStep>
{
    private readonly ICatalogManager<OmnichannelCampaignGroup> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="OmnichannelCampaignGroupDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the campaign groups.</param>
    public OmnichannelCampaignGroupDeploymentSource(ICatalogManager<OmnichannelCampaignGroup> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(OmnichannelCampaignGroupDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(OmnichannelDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = OmnichannelDeploymentSteps.CampaignGroup,
            ["CampaignGroups"] = data,
        });
    }
}
