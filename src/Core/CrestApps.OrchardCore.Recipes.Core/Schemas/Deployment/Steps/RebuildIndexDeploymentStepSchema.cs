using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>RebuildIndexDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class RebuildIndexDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "RebuildIndexDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Index rebuild";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to rebuild the selected index profiles.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("index profiles"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the index profiles to rebuild.", context.Examples.IndexProfileNames));
    }
}
