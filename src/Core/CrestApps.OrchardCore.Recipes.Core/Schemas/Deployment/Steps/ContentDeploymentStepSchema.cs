using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ContentDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ContentDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ContentDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Content";

    /// <inheritdoc />
    protected override string Description => "Exports the content items of the selected content types.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ContentTypes", DeploymentSchemaBuilders.StringArray("The technical names of the content types whose items are exported.", context.Examples.ContentTypeNames));
        yield return ("ExportAsSetupRecipe", DeploymentSchemaBuilders.Boolean("When true, exports the content in a form suitable for a setup recipe, resetting owner and identifier information."));
    }
}
