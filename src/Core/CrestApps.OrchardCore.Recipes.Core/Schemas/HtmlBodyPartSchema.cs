using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the html body part schema.
/// </summary>
public sealed class HtmlBodyPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "HtmlBodyPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("HtmlBodyPartSettings",
            Obj(
                Prop("SanitizeHtml", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(true)
                    .Description("Sanitize HTML input. Liquid is disabled when true.")))
            );
    }
}
