using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Teams.Deployments;

internal sealed class TeamsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(TeamsDeploymentStep);

    protected override string DisplayText => "Teams";

    protected override string Description => "Exports every team, with its parent by name.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        return [];
    }
}
