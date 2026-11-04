using CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment;
using Json.Schema;

namespace CrestApps.OrchardCore.AI.FileSources.Deployments;

/// <summary>
/// Describes the file sources deployment step to CrestApps Recipes.
/// </summary>
internal sealed class FileSourcesDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    public override string StepType => nameof(FileSourcesDeploymentStep);

    protected override string DisplayText => "File sources";

    protected override string Description => "Exports every file source with its connector settings. Passwords, keys and other secrets are never exported.";

    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield break;
    }
}
