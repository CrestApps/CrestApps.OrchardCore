using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Deployment.Steps;

/// <summary>
/// Describes the <c>SiteSettingsDeploymentStep</c> deployment step payload.
/// </summary>
public sealed class SiteSettingsDeploymentStepSchema : DeploymentStepSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string StepType => "SiteSettingsDeploymentStep";

    /// <inheritdoc />
    protected override string DisplayText => "Site settings";

    /// <inheritdoc />
    protected override string Description => "Exports the selected sections of the site settings.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(DeploymentStepSchemaContext context)
    {
        yield return ("Settings", DeploymentSchemaBuilders.StringArray("The names of the site settings sections to export, for example BaseUrl or TimeZoneId."));
    }
}
