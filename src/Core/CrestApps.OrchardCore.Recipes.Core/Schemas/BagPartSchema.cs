using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the bag part schema.
/// </summary>
public sealed class BagPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "BagPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("BagPartSettings",
            Obj(
                Prop("ContainedContentTypes", StringArray()),
                Prop("ContainedStereotypes", StringArray()),
                Prop("DisplayType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                Prop("CollapseContainedItems", BoolProp()))
            );
    }
}
