using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for content culture picker settings.
/// </summary>
public sealed class ContentCulturePickerSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "ContentCulturePickerSettings";

    /// <summary>
    /// Builds the schema for content culture picker settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the content culture picker behavior.")
            .Properties(
                ("RedirectToHomepage", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to redirect to the homepage when switching cultures.")),
                ("SetCookie", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to set a cookie to remember the selected culture.").Default(true)))
            .AdditionalProperties(false);
}
