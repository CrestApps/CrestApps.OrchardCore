using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

public sealed class ContainedPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "ContainedPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ListContentItemId", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("ListContentType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Order", new JsonSchemaBuilder().Type(SchemaValueType.Integer)))
            .AdditionalProperties(true);
    }
}
