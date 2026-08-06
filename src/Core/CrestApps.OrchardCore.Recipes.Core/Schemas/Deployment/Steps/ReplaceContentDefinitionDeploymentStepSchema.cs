using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>ReplaceContentDefinitionDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class ReplaceContentDefinitionDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "ReplaceContentDefinitionDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Replace content definitions";

    /// <inheritdoc />
    protected override string Description => "Exports the selected content type and content part definitions so they replace, rather than merge with, the definitions on the target.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("content type and content part definitions"));
        yield return ("ContentTypes", DeploymentSchemaBuilders.StringArray("The technical names of the content type definitions to export."));
        yield return ("ContentParts", DeploymentSchemaBuilders.StringArray("The technical names of the content part definitions to export."));
    }
}
