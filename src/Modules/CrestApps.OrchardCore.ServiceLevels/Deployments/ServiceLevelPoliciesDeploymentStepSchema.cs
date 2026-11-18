using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.ServiceLevels.Deployments;

/// <summary>
/// Describes the service level policies deployment step to CrestApps Recipes.
/// </summary>
internal sealed class ServiceLevelPoliciesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(ServiceLevelPoliciesDeploymentStep);

    protected override string DisplayText => "Service level policies";

    protected override string Description => "Exports every service level policy.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
