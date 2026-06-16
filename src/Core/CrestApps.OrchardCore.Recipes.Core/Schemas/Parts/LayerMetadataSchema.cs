using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the layer metadata schema.
/// </summary>
public sealed class LayerMetadataSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "LayerMetadata";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("RenderTitle", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("Position", new JsonSchemaBuilder().Type(SchemaValueType.Number)),
                ("Zone", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Layer", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }
}
