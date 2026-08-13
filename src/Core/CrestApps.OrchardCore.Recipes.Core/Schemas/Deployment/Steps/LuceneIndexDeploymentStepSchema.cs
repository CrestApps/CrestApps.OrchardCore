using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>LuceneIndexDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class LuceneIndexDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "LuceneIndexDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Lucene index settings";

    /// <inheritdoc />
    protected override string Description => "Exports the settings of the selected Lucene indexes.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Lucene indexes"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the Lucene indexes to export.", context.Examples.IndexProfileNames));
    }
}
