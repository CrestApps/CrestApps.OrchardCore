using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>DeleteContentDefinitionDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class DeleteContentDefinitionDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "DeleteContentDefinitionDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Delete content definitions";

    /// <inheritdoc />
    protected override string Description => "Deletes the selected content type and content part definitions on the target.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("ContentTypes", DeploymentSchemaBuilders.StringArray("The technical names of the content type definitions to delete."));
        yield return ("ContentParts", DeploymentSchemaBuilders.StringArray("The technical names of the content part definitions to delete."));
    }
}
