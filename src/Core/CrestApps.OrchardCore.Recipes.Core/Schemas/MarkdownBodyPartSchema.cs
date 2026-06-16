using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the markdown body part schema.
/// </summary>
public sealed class MarkdownBodyPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "MarkdownBodyPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("MarkdownBodyPartSettings",
            Obj(
                Prop("SanitizeHtml", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Boolean)
                    .Default(true)
                    .Description("Sanitize rendered HTML output.")))
            );
    }
}
