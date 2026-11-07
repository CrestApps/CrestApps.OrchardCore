using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Inventory.Deployments;

/// <summary>
/// Describes the stock locations deployment step to CrestApps Recipes.
/// </summary>
internal sealed class StockLocationsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(StockLocationsDeploymentStep);

    protected override string DisplayText => "Stock locations";

    protected override string Description => "Exports the store's own stock locations, with their ids, so stock and registers keep pointing at them.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
