using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the autoroute part schema.
/// </summary>
public sealed class AutoroutePartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "AutoroutePart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AutoroutePartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AllowCustomPath", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Default("{{ ContentItem.DisplayText | slugify }}")
                            .Description("The pattern used to build the Path. Must be valid Liquid syntax.")),
                        ("ShowHomepageOption", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowUpdatePath", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowDisabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowRouteContainedItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("ManageContainedItemRoutes", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowAbsolutePath", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Path", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("SetHomepage", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Disabled", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("RouteContainedItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Absolute", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
            .AdditionalProperties(true);
    }
}
