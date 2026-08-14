using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the geo point field schema.
/// </summary>
public sealed class GeoPointFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "GeoPointField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("GeoPointFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Latitude", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder().Type(SchemaValueType.Number),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))),
                ("Longitude", new JsonSchemaBuilder().AnyOf(
                    new JsonSchemaBuilder().Type(SchemaValueType.Number),
                    new JsonSchemaBuilder().Type(SchemaValueType.Null))))
            .AdditionalProperties(true);
    }
}
