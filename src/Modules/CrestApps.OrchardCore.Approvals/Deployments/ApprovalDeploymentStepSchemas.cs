using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.Approvals.Deployments;

internal sealed class ApprovalPoliciesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ApprovalPoliciesDeploymentStep);

    protected override string DisplayText => "Approval policies";

    protected override string Description => "Exports every approval policy.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}

internal sealed class ApprovalRequestsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ApprovalRequestsDeploymentStep);

    protected override string DisplayText => "Approval requests";

    protected override string Description => "Exports the approval requests the person running the plan may see; the importing site keeps them as history.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
