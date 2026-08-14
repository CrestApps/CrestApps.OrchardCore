using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the date time field schema.
/// </summary>
public sealed class DateTimeFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "DateTimeField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("DateTimeFieldSettings", new JsonSchemaBuilder()
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
                        .Description("An ISO-8601 UTC date/time value."),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
}
