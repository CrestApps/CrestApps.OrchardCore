using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.TenantHierarchy.Deployments;

/// <summary>
/// Describes the child tenant access deployment step to CrestApps Recipes.
/// </summary>
internal sealed class AccessGrantsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(AccessGrantsDeploymentStep);

    protected override string DisplayText => "Child tenant access";

    protected override string Description => "Exports who in the parent tenant may sign in to child tenants, and with which roles there.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
