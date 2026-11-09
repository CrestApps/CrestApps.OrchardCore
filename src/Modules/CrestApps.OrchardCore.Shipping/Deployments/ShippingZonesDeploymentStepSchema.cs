using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Shipping.Deployments;

/// <summary>
/// Describes the shipping zones deployment step to CrestApps Recipes.
/// </summary>
internal sealed class ShippingZonesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ShippingZonesDeploymentStep);

    protected override string DisplayText => "Shipping zones";

    protected override string Description => "Exports every shipping zone with its countries, regions and shipping methods.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
