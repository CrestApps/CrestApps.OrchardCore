using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the text field schema.
/// </summary>
public sealed class TextFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "TextField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("TextFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Type", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Editable", "GeneratedDisabled", "GeneratedHidden")),
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("This string must be valid Liquid syntax when provided.")),
                        ("Placeholder", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)),
                ("TextFieldHeaderDisplaySettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Level", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)),
                ("TextFieldMonacoEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)),
                ("TextFieldPredefinedListEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Options", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("value", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                                .AdditionalProperties(false))),
                        ("Editor", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Radio", "Dropdown")),
                        ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Text", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
