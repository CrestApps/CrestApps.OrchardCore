using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the alias part schema.
/// </summary>
public sealed class AliasPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "AliasPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("AliasPartSettings",
            Obj(
                Prop("Pattern", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Default("{{ Model.ContentItem.DisplayText | slugify }}")
                    .Description("Pattern for generating the alias. Must be valid Liquid syntax.")),
                Prop("Options", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .Enum("Editable", "GeneratedDisabled")
                    .Default("Editable")
                    .Description("Whether the alias is editable or auto-generated.")))
            );
    }
}
