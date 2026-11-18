using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Checks.Deployments;

internal sealed class CheckRegisterDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(CheckRegisterDeploymentStep);

    protected override string DisplayText => "Check register";

    protected override string Description => "Exports every check of the register; the importing site keeps them as history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
