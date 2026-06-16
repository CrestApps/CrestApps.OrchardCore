using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the markdown field schema.
/// </summary>
public sealed class MarkdownFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "MarkdownField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("MarkdownFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("SanitizeHtml", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("RenderLiquid", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("MarkdownFieldWysiwygEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Markdown", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
