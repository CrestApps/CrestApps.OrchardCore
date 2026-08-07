using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>IndexProfileDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class IndexProfileDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "IndexProfileDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Index profile settings";

    /// <inheritdoc />
    protected override string Description => "Exports the settings of the selected index profiles.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("index profiles"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the index profiles to export.", context.Examples.IndexProfileNames));
    }
}
