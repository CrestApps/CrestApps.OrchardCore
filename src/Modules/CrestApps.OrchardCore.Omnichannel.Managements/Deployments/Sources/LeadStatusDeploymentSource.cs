using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

internal sealed class LeadStatusDeploymentSource : DeploymentSourceBase<LeadStatusDeploymentStep>
{
    private readonly INamedCatalogManager<LeadStatus> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="LeadStatusDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the lead statuses.</param>
    public LeadStatusDeploymentSource(INamedCatalogManager<LeadStatus> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(LeadStatusDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(OmnichannelDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = OmnichannelDeploymentSteps.LeadStatus,
            ["LeadStatuses"] = data,
        });
    }
}
