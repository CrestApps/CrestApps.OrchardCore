using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Describes the store credit deployment step to CrestApps Recipes.
/// </summary>
internal sealed class StoreCreditAccountsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(StoreCreditAccountsDeploymentStep);

    protected override string DisplayText => "Store credit";

    protected override string Description => "Exports every customer's store credit, with its balance and activity.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
