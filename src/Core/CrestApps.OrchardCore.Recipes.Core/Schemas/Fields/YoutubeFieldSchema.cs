using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Represents the YouTube field schema.
/// </summary>
public sealed class YoutubeFieldSchema : FieldSchemaDefinitionBase
{
    public override string Name { get; } = "YoutubeField";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("YoutubeFieldSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Hint", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Required", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("Label", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("Width", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                        ("Height", new JsonSchemaBuilder().Type(SchemaValueType.Integer)),
                        ("Placeholder", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RawAddress", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("EmbeddedAddress", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }
}
