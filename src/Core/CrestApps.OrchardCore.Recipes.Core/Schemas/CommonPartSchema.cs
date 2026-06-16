using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the common part schema.
/// </summary>
public sealed class CommonPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "CommonPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("CommonPartSettings",
            Obj(
                Prop("DisplayDateEditor", BoolProp()),
                Prop("DisplayOwnerEditor", BoolProp()))
            );
    }
}
