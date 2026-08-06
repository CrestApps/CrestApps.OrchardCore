using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>AzureAISearchIndexDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class AzureAISearchIndexDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "AzureAISearchIndexDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Azure AI Search index settings";

    /// <inheritdoc />
    protected override string Description => "Exports the settings of the selected Azure AI Search indices.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("Azure AI Search indices"));
        yield return ("IndexNames", DeploymentSchemaBuilders.StringArray("The names of the Azure AI Search indices to export."));
    }
}
