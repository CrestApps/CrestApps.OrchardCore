using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the autoroute part schema.
/// </summary>
public sealed class AutoroutePartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "AutoroutePart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("AutoroutePartSettings",
            Obj(
                Prop("AllowCustomPath", BoolProp()),
                Prop("Pattern", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Default("{{ ContentItem.DisplayText | slugify }}")
                    .Description("The pattern used to build the Path. Must be valid Liquid syntax.")),
                Prop("ShowHomepageOption", BoolProp()),
                Prop("AllowUpdatePath", BoolProp()),
                Prop("AllowDisabled", BoolProp()),
                Prop("AllowRouteContainedItems", BoolProp()),
                Prop("ManageContainedItemRoutes", BoolProp()),
                Prop("AllowAbsolutePath", BoolProp()))
            );
    }
}
