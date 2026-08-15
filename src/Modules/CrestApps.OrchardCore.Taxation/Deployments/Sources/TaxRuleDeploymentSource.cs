using System.Text.Json.Nodes;
using CrestApps.Core.Services;
using CrestApps.OrchardCore.Taxation.Deployments.Steps;
using CrestApps.OrchardCore.Taxation.Models;
using OrchardCore.Deployment;

namespace CrestApps.OrchardCore.Taxation.Deployments.Sources;

internal sealed class TaxRuleDeploymentSource : DeploymentSourceBase<TaxRuleDeploymentStep>
{
    private readonly INamedCatalogManager<TaxRule> _manager;

    public TaxRuleDeploymentSource(INamedCatalogManager<TaxRule> manager)
    {
        _manager = manager;
    }

    protected override async Task ProcessAsync(TaxRuleDeploymentStep step, DeploymentPlanResult result)
    {
        var entries = await _manager.GetAllAsync();

        var data = new JsonArray();

        foreach (var entry in entries)
        {
            data.Add(TaxationDeploymentSerializer.Export(entry));
        }

        result.Steps.Add(new JsonObject
        {
            ["name"] = TaxationDeploymentSteps.TaxRule,
            ["TaxRules"] = data,
        });
    }
}
