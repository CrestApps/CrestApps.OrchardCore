using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the widgets list part schema.
/// </summary>
public sealed class WidgetsListPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "WidgetsListPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("WidgetsListPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("Zones", new JsonSchemaBuilder()
                            .Type(SchemaValueType.Array)
                            .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }
}
