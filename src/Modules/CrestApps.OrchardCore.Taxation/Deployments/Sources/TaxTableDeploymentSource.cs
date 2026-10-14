using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

internal sealed class TaxTableDeploymentSource : DeploymentSourceBase<TaxTableDeploymentStep>
{
    private readonly INamedCatalogManager<TaxTable> _manager;

    public TaxTableDeploymentSource(INamedCatalogManager<TaxTable> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(TaxTableDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(TaxationDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = TaxationDeploymentSteps.TaxTable,
            ["TaxTables"] = data,
        });
    }
}
