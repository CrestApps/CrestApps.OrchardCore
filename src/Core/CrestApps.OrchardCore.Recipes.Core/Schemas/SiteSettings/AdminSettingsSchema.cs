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
                ("DisplayQuickNavigation", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the quick navigation palette (Ctrl+K / Cmd+K) in the admin navbar.").Default(true)),
                ("DisplayNewMenu", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the 'New' menu in the admin navigation.")),
                ("DisplayTitlesInTopbar", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display page titles in the top bar.")),
                ("ShowBreadcrumb", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether to display the breadcrumb trail above admin page titles.").Default(true)),
                ("MenuBehavior", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Focused", "Persistent").Default("Focused").Description("How the sections of the admin menu open and close.")),
                ("ListLayout", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("List", "Grid").Description("The layout of admin lists. When omitted, AdminListOptions.DefaultLayout is used.")),
                ("ActionsLayout", new JsonSchemaBuilder().Type(SchemaValueType.String).Enum("Buttons", "Menu").Description("The layout of the row actions of admin lists and of the secondary actions of the bottom action bar of edit pages. When omitted, AdminListOptions.DefaultActionsLayout is used.")),
                ("AllowUserListLayoutSelection", new JsonSchemaBuilder().Type(SchemaValueType.Boolean).Description("Whether each admin list shows a layout selector whose choice is kept per user, per list, in a cookie.")))
            .AdditionalProperties(false);
}
