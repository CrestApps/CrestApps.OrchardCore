using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the numeric field schema.
/// </summary>
public sealed class NumericFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "NumericField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("NumericFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Scale", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                        ("Minimum", new JsonSchemaBuilder().AnyOf(
                            new JsonSchemaBuilder().Type(SchemaValueType.Number),
                            new JsonSchemaBuilder().Type(SchemaValueType.Null))),
                        ("Maximum", new JsonSchemaBuilder().AnyOf(
                            new JsonSchemaBuilder().Type(SchemaValueType.Number),
                            new JsonSchemaBuilder().Type(SchemaValueType.Null))),
                        ("Placeholder", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("DefaultValue", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Value", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder().Type(SchemaValueType.Number),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
}
