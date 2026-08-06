using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>TranslationsDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class TranslationsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "TranslationsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Data translations";

    /// <inheritdoc />
    protected override string Description => "Exports the data localization translations for the selected cultures and categories.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("cultures and categories"));
        yield return ("Cultures", DeploymentSchemaBuilders.StringArray("The culture codes whose translations are exported, for example fr-FR."));
        yield return ("Categories", DeploymentSchemaBuilders.StringArray("The translation categories to export."));
    }
}
