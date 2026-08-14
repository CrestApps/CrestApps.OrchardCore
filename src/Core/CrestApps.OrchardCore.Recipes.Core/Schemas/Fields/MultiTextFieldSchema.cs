using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the multi text field schema.
/// </summary>
public sealed class MultiTextFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "MultiTextField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("MultiTextFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Options", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder()
                                .Type(SchemaValueType.Object)
                                .Properties(
                                    ("name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("value", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                                    ("default", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                                .AdditionalProperties(false))))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Values", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
            .AdditionalProperties(true);
}
