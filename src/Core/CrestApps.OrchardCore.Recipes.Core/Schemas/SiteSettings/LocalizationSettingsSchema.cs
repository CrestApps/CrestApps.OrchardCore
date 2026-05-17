using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for localization settings.
/// </summary>
public sealed class LocalizationSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "LocalizationSettings";

    /// <summary>
    /// Builds the schema for localization settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for site localization and culture settings.")
            .Properties(
                ("DefaultCulture", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The default culture for the site (e.g., en-US).")),
                ("SupportedCultures", new JsonSchemaBuilder().Type(SchemaValueType.Array).Description("The list of supported cultures.").Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("FallBackToParentCulture", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to fall back to the parent culture when a translation is not found.")))
            .AdditionalProperties(false);
}
