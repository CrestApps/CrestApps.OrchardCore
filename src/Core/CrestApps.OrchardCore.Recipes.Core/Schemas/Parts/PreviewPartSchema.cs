using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the preview part schema.
/// </summary>
public sealed class PreviewPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "PreviewPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PreviewPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Description("Pattern for building the preview path or display content.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }
}
