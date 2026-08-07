using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>LuceneIndexRebuildDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class LuceneIndexRebuildDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "LuceneIndexRebuildDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Lucene index rebuild";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to rebuild the selected Lucene indexes.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Lucene indexes"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the Lucene indexes to rebuild.", context.Examples.IndexProfileNames));
    }
}
