using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;

/// <summary>
/// Provides the schema definition for admin settings.
/// </summary>
public sealed class AdminSettingsSchema : SiteSettingsSchemaBase
{
    /// <summary>
    /// Gets the property name exposed by the <c>Settings</c> recipe step.
    /// </summary>
    public override string Name => "AdminSettings";

    /// <summary>
    /// Builds the schema for admin settings.
    /// </summary>
    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("Configuration for the admin dashboard appearance and behavior.")
            .Properties(
                ("DisplayThemeToggler", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the light/dark theme toggler in the admin.").Default(true)),
                ("DisplayMenuFilter", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the menu filter input in the admin navigation.")),
                ("DisplayNewMenu", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the 'New' menu in the admin navigation.")),
                ("DisplayTitlesInTopbar", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display page titles in the top bar.")))
            .AdditionalProperties(false);
}
