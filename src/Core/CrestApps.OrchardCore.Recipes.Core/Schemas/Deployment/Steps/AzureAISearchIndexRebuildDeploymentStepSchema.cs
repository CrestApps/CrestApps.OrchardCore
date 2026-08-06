using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AzureAISearchIndexRebuildDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AzureAISearchIndexRebuildDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AzureAISearchIndexRebuildDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Azure AI Search index rebuild";

    /// <inheritdoc />
    protected override string Description => "Instructs the target to rebuild the selected Azure AI Search indices.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Azure AI Search indices"));
        yield return ("Indices", DeploymentSchemaBuilders.StringArray("The names of the Azure AI Search indices to rebuild."));
    }
}
