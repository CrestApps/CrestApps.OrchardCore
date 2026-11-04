using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Customers.Deployments;

/// <summary>
/// Describes the loyalty points deployment step to CrestApps Recipes.
/// </summary>
internal sealed class LoyaltyAccountsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(LoyaltyAccountsDeploymentStep);

    protected override string DisplayText => "Loyalty points";

    protected override string Description => "Exports every customer's loyalty points, with their activity.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
