using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Steps;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Omnichannel.Managements.Deployments.Sources;

internal sealed class CadenceDeploymentSource : DeploymentSourceBase<CadenceDeploymentStep>
{
    private readonly ICatalogManager<Cadence> _manager;

    /// <summary>
    /// Initializes a new instance of the <see cref="CadenceDeploymentSource"/> class.
    /// </summary>
    /// <param name="manager">The manager that owns the cadences.</param>
    public CadenceDeploymentSource(ICatalogManager<Cadence> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(CadenceDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(OmnichannelDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = OmnichannelDeploymentSteps.Cadence,
            ["Cadences"] = data,
        });
    }
}
