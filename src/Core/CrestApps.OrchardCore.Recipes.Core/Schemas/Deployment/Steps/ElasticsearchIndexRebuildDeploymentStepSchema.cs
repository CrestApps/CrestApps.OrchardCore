using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ElasticsearchIndexRebuildDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ElasticsearchIndexRebuildDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ElasticsearchIndexRebuildDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Elasticsearch index rebuild";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to rebuild the selected Elasticsearch indices.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Elasticsearch indices"));
        yield return ("Indices", DeploymentSchemaBuilders.StringArray("The names of the Elasticsearch indices to rebuild."));
    }
}
