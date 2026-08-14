using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the HTML field schema.
/// </summary>
public sealed class HtmlFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "HtmlField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("HtmlFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("SanitizeHtml", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("RenderLiquid", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("HtmlFieldMonacoEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)),
                ("HtmlFieldTrumbowygEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("InsertMediaWithUrl", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Html", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
