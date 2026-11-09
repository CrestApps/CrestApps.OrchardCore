using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Fields;

/// <summary>
/// Provides the schema definition for the Liquid field, which renders a Liquid template.
/// </summary>
public sealed class LiquidFieldSchema : FieldSchemaDefinitionBase
{
    /// <summary>
    /// Gets the name of the field.
    /// </summary>
    public override string Name { get; } = "LiquidField";

    protected override JsonSchemaBuilder BuildSettingsCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .AdditionalProperties(true);

    protected override JsonSchemaBuilder BuildFieldSchemaCore()
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(("Liquid", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("The Liquid template rendered by the field.")))
            .AdditionalProperties(true);
}
