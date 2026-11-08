using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Checkout.Deployments;

/// <summary>
/// Describes the coupons deployment step to CrestApps Recipes.
/// </summary>
internal sealed class CouponsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CouponsDeploymentStep);

    protected override string DisplayText => "Coupons";

    protected override string Description => "Exports every coupon and automatic discount, without how often it was used.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
