namespace CrestApps.OrchardCore.Recipes.Core.Schemas;

/// <summary>All built-in part settings schema definitions, grouped in a single compilation unit.</summary>

public sealed class CommonPartSchema : PartSettingsSchemaBase
{
    public override string Name => "CommonPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("CommonPartSettings",
        Obj(
            Prop("DisplayDateEditor", BoolProp()),
            Prop("DisplayOwnerEditor", BoolProp())));
}

public sealed class TitlePartSchema : PartSettingsSchemaBase
{
    public override string Name => "TitlePart";

    protected override JsonSchema BuildSettingsCore() => Envelope("TitlePartSettings",
        Obj(
            Prop("RenderTitle", BoolProp()),
            Prop("Options", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Enum("Editable", "GeneratedDisabled", "GeneratedHidden", "EditableRequired")
                .Default("Editable")),
            Prop("Pattern", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("This string must be a valid Liquid syntax"))));
}

public sealed class AutoroutePartSchema : PartSettingsSchemaBase
{
    public override string Name => "AutoroutePart";

    protected override JsonSchema BuildSettingsCore() => Envelope("AutoroutePartSettings",
        Obj(
            Prop("AllowCustomPath", BoolProp()),
            Prop("Pattern", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Default("{{ ContentItem.DisplayText | slugify }}")
                .Description("The pattern used to build the Path. Must be valid Liquid syntax.")),
            Prop("ShowHomepageOption", BoolProp()),
            Prop("AllowUpdatePath", BoolProp()),
            Prop("AllowDisabled", BoolProp()),
            Prop("AllowRouteContainedItems", BoolProp()),
            Prop("ManageContainedItemRoutes", BoolProp()),
            Prop("AllowAbsolutePath", BoolProp())));
}

public sealed class AliasPartSchema : PartSettingsSchemaBase
{
    public override string Name => "AliasPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("AliasPartSettings",
        Obj(
            Prop("Pattern", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Default("{{ Model.ContentItem.DisplayText | slugify }}")
                .Description("Pattern for generating the alias. Must be valid Liquid syntax.")),
            Prop("Options", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Enum("Editable", "GeneratedDisabled")
                .Default("Editable")
                .Description("Whether the alias is editable or auto-generated."))));
}

public sealed class HtmlBodyPartSchema : PartSettingsSchemaBase
{
    public override string Name => "HtmlBodyPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("HtmlBodyPartSettings",
        Obj(
            Prop("SanitizeHtml", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Default(true)
                .Description("Sanitize HTML input. Liquid is disabled when true."))));
}

public sealed class MarkdownBodyPartSchema : PartSettingsSchemaBase
{
    public override string Name => "MarkdownBodyPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("MarkdownBodyPartSettings",
        Obj(
            Prop("SanitizeHtml", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Default(true)
                .Description("Sanitize rendered HTML output."))));
}

public sealed class ListPartSchema : PartSettingsSchemaBase
{
    public override string Name => "ListPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("ListPartSettings",
        Obj(
            Prop("PageSize", new JsonSchemaBuilder().Type(SchemaValueType.Integer).Default(10)),
            Prop("ContainedContentTypes", StringArray()),
            Prop("EnableOrdering", BoolProp()),
            Prop("ShowHeader", BoolProp())));
}

public sealed class FlowPartSchema : PartSettingsSchemaBase
{
    public override string Name => "FlowPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("FlowPartSettings",
        Obj(
            Prop("ContainedContentTypes", StringArray()),
            Prop("CollapseContainedItems", BoolProp())));
}

public sealed class BagPartSchema : PartSettingsSchemaBase
{
    public override string Name => "BagPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("BagPartSettings",
        Obj(
            Prop("ContainedContentTypes", StringArray()),
            Prop("ContainedStereotypes", StringArray()),
            Prop("DisplayType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
            Prop("CollapseContainedItems", BoolProp())));
}

public sealed class WidgetsListPartSchema : PartSettingsSchemaBase
{
    public override string Name => "WidgetsListPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("WidgetsListPartSettings",
        Obj(Prop("Zones", StringArray())));
}

public sealed class PreviewPartSchema : PartSettingsSchemaBase
{
    public override string Name => "PreviewPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("PreviewPartSettings",
        Obj(
            Prop("Pattern", new JsonSchemaBuilder()
                .Type(SchemaValueType.String)
                .Description("Pattern for building the preview path or display content."))));
}

public sealed class SeoMetaPartSchema : PartSettingsSchemaBase
{
    public override string Name => "SeoMetaPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("SeoMetaPartSettings",
        Obj(
            Prop("DisplayKeywords", BoolProp()),
            Prop("DisplayCustomMetaTags", BoolProp()),
            Prop("DisplayOpenGraph", BoolProp()),
            Prop("DisplayTwitter", BoolProp()),
            Prop("DisplayGoogleSchema", BoolProp())));
}

public sealed class AuditTrailPartSchema : PartSettingsSchemaBase
{
    public override string Name => "AuditTrailPart";

    protected override JsonSchema BuildSettingsCore() => Envelope("AuditTrailPartSettings",
        Obj(
            Prop("ShowCommentInput", new JsonSchemaBuilder()
                .Type(SchemaValueType.Boolean)
                .Default(true)
                .Description("Show the comment input field."))));
}
