using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the title part schema.
/// </summary>
public sealed class TitlePartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "TitlePart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("TitlePartSettings",
            Obj(
                Prop("RenderTitle", BoolProp()),
                Prop("Options", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Editable", "GeneratedDisabled", "GeneratedHidden", "EditableRequired")
                    .Default("Editable")),
                Prop("Pattern", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Description("This string must be a valid Liquid syntax")))
            );
    }
}
