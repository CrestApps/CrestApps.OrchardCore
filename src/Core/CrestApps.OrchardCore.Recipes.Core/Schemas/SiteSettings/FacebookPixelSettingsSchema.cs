using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for Facebook Pixel settings.
/// </summary>
public sealed class FacebookPixelSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "FacebookPixelSettings";

    /// <summary>
    /// Builds the schema for Facebook Pixel settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for Meta Pixel tracking.")
            .Properties(
                ("PixelId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The Meta Pixel identifier.")))
            .Required("PixelId")
            .AdditionalProperties(false);
}
