using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Google Tag Manager settings.
/// </summary>
public sealed class GoogleTagManagerSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "GoogleTagManagerSettings";

    /// <summary>
    /// Builds the schema for Google Tag Manager settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Google Tag Manager.")
            .Properties(
                ("ContainerID", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Google Tag Manager container ID (e.g., GTM-XXXXXXX).")))
            .Required("ContainerID")
            .AdditionalProperties(false);
}
