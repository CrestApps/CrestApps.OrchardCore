using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AllAdminTemplatesDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AllAdminTemplatesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AllAdminTemplatesDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "All admin templates";

    /// <inheritdoc />
    protected override string Description => "Exports every admin template.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ExportAsFiles", DeploymentSchemaBuilders.Boolean("When true, exports each admin template as a separate file rather than embedding them in the recipe."));
    }
}
