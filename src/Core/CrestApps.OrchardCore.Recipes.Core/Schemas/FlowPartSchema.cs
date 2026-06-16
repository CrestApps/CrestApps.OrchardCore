using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the flow part schema.
/// </summary>
public sealed class FlowPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "FlowPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("FlowPartSettings",
            Obj(
                Prop("ContainedContentTypes", StringArray()),
                Prop("CollapseContainedItems", BoolProp()))
            );
    }
}
