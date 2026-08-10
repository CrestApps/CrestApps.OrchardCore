using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>CustomUserSettingsDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class CustomUserSettingsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "CustomUserSettingsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Custom user settings";

    /// <inheritdoc />
    protected override string Description => "Exports the custom user settings for the selected settings types.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("IncludeAll", DeploymentSchemaBuilders.IncludeAll("custom user settings types"));
        yield return ("SettingsTypeNames", DeploymentSchemaBuilders.StringArray("The technical names of the custom user settings content types to export."));
    }
}
