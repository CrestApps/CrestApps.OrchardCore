using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>
/// Represents the seo meta part schema.
/// </summary>
public sealed class SeoMetaPartSchema : PartSettingsSchemaBase
{
    public override string Name { get; } = "SeoMetaPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return Envelope("SeoMetaPartSettings",
            Obj(
                Prop("DisplayKeywords", BoolProp()),
                Prop("DisplayCustomMetaTags", BoolProp()),
                Prop("DisplayOpenGraph", BoolProp()),
                Prop("DisplayTwitter", BoolProp()),
                Prop("DisplayGoogleSchema", BoolProp()))
            );
    }
}
