using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>DeploymentPlanDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class DeploymentPlanDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "DeploymentPlanDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Deployment plans";

    /// <inheritdoc />
    protected override string Description => "Exports the selected deployment plans.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("deployment plans"));
        yield return ("DeploymentPlanNames", DeploymentSchemaBuilders.StringArray("The names of the deployment plans to export."));
    }
}
