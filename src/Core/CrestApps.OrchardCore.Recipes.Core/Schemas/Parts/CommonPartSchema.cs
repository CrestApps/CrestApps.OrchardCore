using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the common part schema.
/// </summary>
public sealed class CommonPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "CommonPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("CommonPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayDateEditor", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayOwnerEditor", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }
}
