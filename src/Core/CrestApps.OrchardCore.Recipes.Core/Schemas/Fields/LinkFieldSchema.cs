using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the link field schema.
/// </summary>
public sealed class LinkFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "LinkField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("LinkFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("HintLinkText", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("LinkTextMode", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Optional", "Required", "Static", "Url")),
                        ("UrlPlaceholder", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("TextPlaceholder", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DefaultUrl", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DefaultText", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DefaultTarget", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Url", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Text", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Target", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }
}
