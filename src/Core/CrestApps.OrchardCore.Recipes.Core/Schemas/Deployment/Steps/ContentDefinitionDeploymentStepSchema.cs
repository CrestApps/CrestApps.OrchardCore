using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ContentDefinitionDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ContentDefinitionDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ContentDefinitionDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Content definitions";

    /// <inheritdoc />
    protected override string Description => "Exports the selected content type and content part definitions.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("content type and content part definitions"));
        yield return ("ContentTypes", DeploymentSchemaBuilders.StringArray("The technical names of the content type definitions to export."));
        yield return ("ContentParts", DeploymentSchemaBuilders.StringArray("The technical names of the content part definitions to export."));
    }
}
