using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Provides the schema definition for the Liquid part, which renders a Liquid template as the body of a content item.
/// </summary>
public sealed class LiquidPartSchema : PartSchemaDefinitionBase
{
    /// <summary>
    /// Gets the name of the part.
    /// </summary>
    public override string Name { get; } = "LiquidPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildPartSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Liquid", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The Liquid template rendered as the body of the content item. Since Orchard Core 4.0, only the Liquid part and the Liquid field execute Liquid; HTML and Markdown bodies no longer do.")))
            .AdditionalProperties(true);
}
