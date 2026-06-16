using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the date field schema.
/// </summary>
public sealed class DateFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "DateField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("DateFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Value", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder()
                        .Type(SchemaValueType.String)
                        .Description("An ISO-8601 date value."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
}
