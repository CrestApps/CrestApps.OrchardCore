using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>QueryBasedContentDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class QueryBasedContentDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "QueryBasedContentDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Query based content";

    /// <inheritdoc />
    protected override string Description => "Exports the content items returned by a named query.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("QueryName", DeploymentSchemaBuilders.String("The name of the query whose returned content items are exported."));
        yield return ("QueryParameters", DeploymentSchemaBuilders.String("A JSON object, serialized as a string, holding the parameter values passed to the query."));
        yield return ("ExportAsSetupRecipe", DeploymentSchemaBuilders.Boolean("When true, exports the content in a form suitable for a setup recipe, resetting owner and identifier information."));
    }
}
