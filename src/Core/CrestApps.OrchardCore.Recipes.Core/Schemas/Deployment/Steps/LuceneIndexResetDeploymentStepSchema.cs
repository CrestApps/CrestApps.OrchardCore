using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>LuceneIndexResetDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class LuceneIndexResetDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "LuceneIndexResetDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Lucene index reset";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to reset the selected Lucene indices.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Lucene indices"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the Lucene indices to reset."));
    }
}
