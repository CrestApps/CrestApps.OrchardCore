using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ResetIndexDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ResetIndexDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ResetIndexDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Index reset";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to reset the selected index profiles.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("index profiles"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the index profiles to reset."));
    }
}
