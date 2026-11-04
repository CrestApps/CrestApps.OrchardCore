using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Marketplace.Deployments;

/// <summary>
/// Describes the vendors deployment step to CrestApps Recipes.
/// </summary>
internal sealed class VendorsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(VendorsDeploymentStep);

    protected override string DisplayText => "Vendors";

    protected override string Description => "Exports every marketplace vendor with its users by name and its commission. Payout accounts are not exported.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
