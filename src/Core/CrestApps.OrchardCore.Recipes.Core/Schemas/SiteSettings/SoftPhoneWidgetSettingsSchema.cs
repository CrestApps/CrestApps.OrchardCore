using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for the soft phone widget settings.
/// </summary>
public sealed class SoftPhoneWidgetSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "SoftPhoneWidgetSettings";

    /// <summary>
    /// Builds the schema for the soft phone widget settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Where the soft phone widget is shown and how it looks.")
            .Properties(
                ("Enabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Kill switch for the soft phone widget. When false the widget, its styles and its scripts are not rendered anywhere on the site.")),
                ("DisplayOnAdmin", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Default(true).Description("Whether the floating soft phone widget is shown on the admin dashboard.")),
                ("EnableDiagnostics", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether the soft phone's Diagnostics tab (live media stats, microphone meter, provider warnings and the audio test) is shown.")),
                ("AccentColor", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Default("#2f6fed").Description("Accent color of the widget, as a CSS color value.")),
                ("RecentCallsCount", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Minimum(0).Default(30).Description("Maximum number of calls shown in the recent-calls history.")),
                ("DefaultCountryCode", new JsonSchemaBuilder().Type(SchemaValueType.String | SchemaValueType.Null).Description("ISO 3166-1 alpha-2 country the phone number input selects initially. When empty, the country is derived from the request culture.")))
            .AdditionalProperties(false);
}
