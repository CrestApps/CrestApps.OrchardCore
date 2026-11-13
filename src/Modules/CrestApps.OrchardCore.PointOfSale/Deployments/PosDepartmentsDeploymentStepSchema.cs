using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.PointOfSale.Deployments;

/// <summary>
/// Describes the register departments deployment step to CrestApps Recipes.
/// </summary>
internal sealed class PosDepartmentsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(PosDepartmentsDeploymentStep);

    protected override string DisplayText => "Register departments";

    protected override string Description => "Exports every department registers ring amounts up in, with its number, tax category, key color and limits.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
