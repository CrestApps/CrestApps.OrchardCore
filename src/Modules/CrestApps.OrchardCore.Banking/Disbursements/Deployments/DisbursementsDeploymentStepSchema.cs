using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Banking.Disbursements.Deployments;

internal sealed class DisbursementsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(DisbursementsDeploymentStep);

    protected override string DisplayText => "Disbursements";

    protected override string Description => "Exports the disbursements the person running the plan may see, without account numbers; the importing site keeps them as history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
