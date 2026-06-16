using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the html body part schema.
/// </summary>
public sealed class HtmlBodyPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "HtmlBodyPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("HtmlBodyPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("SanitizeHtml", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Boolean)
                            .Default(true)
                            .Description("Sanitize HTML input.")),
                        ("RenderLiquid", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("HtmlBodyPartMonacoEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("InsertMediaWithUrl", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowCustomScripts", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)),
                ("HtmlBodyPartTrumbowygEditorSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Options", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                        ("InsertMediaWithUrl", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("AllowCustomScripts", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Html", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
