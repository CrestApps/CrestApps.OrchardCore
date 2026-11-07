using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Memberships.Deployments;

/// <summary>
/// Describes the memberships deployment step to CrestApps Recipes.
/// </summary>
internal sealed class MembershipsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(MembershipsDeploymentStep);

    protected override string DisplayText => "Memberships";

    protected override string Description => "Exports every membership with its member by user name, its product, roles and dates.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
