using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AllContentDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AllContentDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AllContentDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "All content";

    /// <inheritdoc />
    protected override string Description => "Exports every content item on the tenant.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ExportAsSetupRecipe", DeploymentSchemaBuilders.Boolean("When true, exports the content in a form suitable for a setup recipe, resetting owner and identifier information."));
    }
}
