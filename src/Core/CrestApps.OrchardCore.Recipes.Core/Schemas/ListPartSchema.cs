using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the list part schema.
/// </summary>
public sealed class ListPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "ListPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("ListPartSettings",
            Obj(
                Prop("PageSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(10)),
                Prop("ContainedContentTypes", StringArray()),
                Prop("EnableOrdering", BoolProp()),
                Prop("ShowHeader", BoolProp()))
            );
    }
}
