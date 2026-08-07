using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>MediaDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class MediaDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "MediaDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Media";

    /// <inheritdoc />
    protected override string Description => "Exports the selected media files and directories.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("media files"));
        yield return ("FilePaths", DeploymentSchemaBuilders.StringArray("The paths of the individual media files to export."));
        yield return ("DirectoryPaths", DeploymentSchemaBuilders.StringArray("The paths of the media directories to export, including their contents."));
    }
}
