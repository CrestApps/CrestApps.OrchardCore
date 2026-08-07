using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ContentItemDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ContentItemDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ContentItemDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Content item";

    /// <inheritdoc />
    protected override string Description => "Exports a single content item selected by its identifier.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["ContentItemId"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ContentItemId", DeploymentSchemaBuilders.String("The identifier of the content item to export."));
    }
}
