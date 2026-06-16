using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the bag part schema.
/// </summary>
public sealed class BagPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "BagPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("BagPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("ContainedContentTypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("ContainedStereotypes", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                        ("DisplayType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("CollapseContainedItems", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("BagPartBlocksEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("AddButtonText", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("ModalTitleText", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                    .AdditionalProperties(false)
                )
            )
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentItems", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .AdditionalProperties(true))))
            .AdditionalProperties(true);
}
