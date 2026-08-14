using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for AI chat admin widget settings.
/// </summary>
public sealed class AIChatAdminWidgetSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AIChatAdminWidgetSettings";

    /// <summary>
    /// Builds the schema for AI chat admin widget settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the AI chat widget displayed in the admin dashboard.")
            .Properties(
                ("ProfileId", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The AI profile identifier used by the admin chat widget.")),
                ("MaxSessions", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Description("The maximum number of concurrent chat sessions allowed.").Default(10)),
                ("PrimaryColor", new JsonSchemaBuilder().Type(SchemaValueType.String).Description("The primary color for the chat widget UI.").Default("#41b670")))
            .AdditionalProperties(false);
}
