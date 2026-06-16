using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the widgets list part schema.
/// </summary>
public sealed class WidgetsListPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "WidgetsListPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("WidgetsListPartSettings",
            Obj(
                Prop("Zones", StringArray()))
            );
    }
}
