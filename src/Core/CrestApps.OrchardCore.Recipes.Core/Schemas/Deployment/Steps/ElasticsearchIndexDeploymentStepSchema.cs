using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ElasticsearchIndexDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ElasticsearchIndexDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ElasticsearchIndexDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Elasticsearch index settings";

    /// <inheritdoc />
    protected override string Description => "Exports the settings of the selected Elasticsearch indexes.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Elasticsearch indexes"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the Elasticsearch indexes to export.", context.Examples.IndexProfileNames));
    }
}
