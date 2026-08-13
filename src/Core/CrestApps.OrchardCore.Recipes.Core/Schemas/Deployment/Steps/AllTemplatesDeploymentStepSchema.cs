using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AllTemplatesDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AllTemplatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AllTemplatesDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "All templates";

    /// <inheritdoc />
    protected override string Description => "Exports every front end template.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ExportAsFiles", DeploymentSchemaBuilders.Boolean("When true, exports each template as a separate file rather than embedding them in the recipe."));
    }
}
