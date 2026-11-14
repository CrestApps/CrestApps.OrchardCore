using CrestApps.OrchardCore.AdminTheme.Models;
using CrestApps.OrchardCore.Recipes.Core.Schemas.SiteSettings;
using Json.Schema;

namespace CrestApps.OrchardCore.AdminTheme.Recipes;

/// <summary>
/// Describes the <c>AdminThemeSettings</c> member of Orchard Core's <c>Settings</c> recipe step to CrestApps Recipes.
/// </summary>
internal sealed class AdminThemeSettingsSchema : SiteSettingsSchemaBase
{
    public override string Name => nameof(AdminThemeSettings);

    protected override JsonSchemaBuilder BuildSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Description("The settings of the CrestApps admin theme.")
            .Properties(
                (nameof(AdminThemeSettings.DefaultColorScheme), new JsonSchemaBuilder()
                    .Type(SchemaValueType.String | SchemaValueType.Null)
                    .Description($"The id of the color scheme of the admin for users who have not picked their own (see the AdminColorSchemes step). Defaults to '{AdminColorSchemes.DefaultId}'.")))
            .AdditionalProperties(true);
}
