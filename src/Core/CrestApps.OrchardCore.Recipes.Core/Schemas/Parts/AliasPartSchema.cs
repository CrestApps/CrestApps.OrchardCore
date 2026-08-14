using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the alias part schema.
/// </summary>
public sealed class AliasPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "AliasPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("AliasPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Pattern", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Default("{{ Model.ContentItem.DisplayText | slugify }}")
                            .Description("Pattern for generating the alias. Must be valid Liquid syntax.")),
                        ("Options", new JsonSchemaBuilder()
                            .Type(SchemaValueType.String)
                            .Enum("Editable", "GeneratedDisabled")
                            .Default("Editable")
                            .Description("Whether the alias is editable or auto-generated.")))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Alias", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
}
