using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Describes the registers deployment step to CrestApps Recipes.
/// </summary>
internal sealed class RegistersDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(RegistersDeploymentStep);

    protected override string DisplayText => "Registers";

    protected override string Description => "Exports every point-of-sale register with its store location, address, currency and roles.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
