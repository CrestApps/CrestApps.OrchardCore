using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Google Analytics settings.
/// </summary>
public sealed class GoogleAnalyticsSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "GoogleAnalyticsSettings";

    /// <summary>
    /// Builds the schema for Google Analytics settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Google Analytics tracking.")
            .Properties(
                ("TrackingID", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Google Analytics tracking ID (e.g., G-XXXXXXXXXX).")))
            .Required("TrackingID")
            .AdditionalProperties(false);
}
