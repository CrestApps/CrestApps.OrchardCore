using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the schema of the <c>AllRateLimitPoliciesDeploymentStep</c> of the Orchard Core Rate Limits module.
/// </summary>
public sealed class AllRateLimitPoliciesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AllRateLimitPoliciesDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "All Rate Limit Policies";

    /// <inheritdoc />
    protected override string Description => "Exports every rate-limit policy as a 'CreateOrUpdateRateLimitPolicies' recipe step.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
        => [];
}
