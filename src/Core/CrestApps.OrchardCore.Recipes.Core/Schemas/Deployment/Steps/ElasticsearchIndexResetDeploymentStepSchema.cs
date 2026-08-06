using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ElasticsearchIndexResetDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ElasticsearchIndexResetDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ElasticsearchIndexResetDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Elasticsearch index reset";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to reset the selected Elasticsearch indices.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Elasticsearch indices"));
        yield return ("Indices", DeploymentSchemaBuilders.StringArray("The names of the Elasticsearch indices to reset."));
    }
}
