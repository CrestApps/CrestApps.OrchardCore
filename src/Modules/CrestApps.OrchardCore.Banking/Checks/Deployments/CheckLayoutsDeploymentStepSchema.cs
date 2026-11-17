using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

internal sealed class CheckLayoutsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CheckLayoutsDeploymentStep);

    protected override string DisplayText => "Check layouts";

    protected override string Description => "Exports every check layout, without its signature images or check line font.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
