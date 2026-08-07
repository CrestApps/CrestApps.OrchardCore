using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>CustomFileDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class CustomFileDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "CustomFileDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Custom file";

    /// <inheritdoc />
    protected override string Description => "Adds an arbitrary text file, with the supplied content, to the deployment package.";

    /// <inheritdoc />
    protected override IEnumerable<string> RequiredProperties => ["FileName"];

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("FileName", DeploymentSchemaBuilders.String("The name of the file written to the deployment package."));
        yield return ("FileContent", DeploymentSchemaBuilders.String("The text content of the file."));
    }
}
