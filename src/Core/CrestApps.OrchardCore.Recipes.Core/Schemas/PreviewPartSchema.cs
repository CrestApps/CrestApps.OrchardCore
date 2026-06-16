using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the preview part schema.
/// </summary>
public sealed class PreviewPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "PreviewPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("PreviewPartSettings",
            Obj(
                Prop("Pattern", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("Pattern for building the preview path or display content.")))
            );
    }
}
