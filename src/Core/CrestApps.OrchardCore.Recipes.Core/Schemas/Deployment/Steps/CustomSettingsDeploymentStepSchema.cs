using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>CustomSettingsDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class CustomSettingsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "CustomSettingsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Custom settings";

    /// <inheritdoc />
    protected override string Description => "Exports the site settings for the selected custom settings types.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("custom settings types"));
        yield return ("SettingsTypeNames", DeploymentSchemaBuilders.StringArray("The technical names of the custom settings content types to export."));
    }
}
