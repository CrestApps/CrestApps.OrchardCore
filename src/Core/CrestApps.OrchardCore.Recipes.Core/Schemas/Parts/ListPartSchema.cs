using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the list part schema.
/// </summary>
public sealed class ListPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "ListPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ListPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("PageSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(10)),
                        ("ContainedContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("EnableOrdering", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("ShowHeader", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("ShowFullPager", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }
}
