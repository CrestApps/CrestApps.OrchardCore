using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Parts;

/// <summary>
/// Represents the seo meta part schema.
/// </summary>
public sealed class SeoMetaPartSchema : PartSchemaDefinitionBase
{
    public override string Name { get; } = "SeoMetaPart";

    protected override JsonSchemaBuilder BuildSettingsCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("SeoMetaPartSettings", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Object)
                    .Properties(
                        ("DisplayKeywords", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayCustomMetaTags", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayOpenGraph", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayTwitter", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                        ("DisplayGoogleSchema", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)))
                    .AdditionalProperties(false)))
            .AdditionalProperties(true);
    }

    protected override JsonSchemaBuilder BuildPartSchemaCore()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("PageTitle", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Render", new JsonSchemaBuilder().Type(SchemaValueType.Boolean)),
                ("MetaDescription", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("MetaKeywords", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("Canonical", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("MetaRobots", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("CustomMetaTags", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("Name", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Property", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Content", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("HttpEquiv", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                            ("Charset", new JsonSchemaBuilder().Type(SchemaValueType.String)))
                        .AdditionalProperties(false))),
                ("DefaultSocialImage", CreateMediaFieldValueSchema()),
                ("OpenGraphImage", CreateMediaFieldValueSchema()),
                ("OpenGraphType", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("OpenGraphTitle", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("OpenGraphDescription", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TwitterImage", CreateMediaFieldValueSchema()),
                ("TwitterTitle", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TwitterDescription", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TwitterCard", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TwitterCreator", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("TwitterSite", new JsonSchemaBuilder().Type(SchemaValueType.String)),
                ("GoogleSchema", new JsonSchemaBuilder().Type(SchemaValueType.String)))
            .AdditionalProperties(true);
    }

    private static JsonSchemaBuilder CreateMediaFieldValueSchema()
    {
        return new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("Paths", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("MediaTexts", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder().Type(SchemaValueType.String))),
                ("Anchors", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Array)
                    .Items(new JsonSchemaBuilder()
                        .Type(SchemaValueType.Object)
                        .Properties(
                            ("X", new JsonSchemaBuilder().Type(SchemaValueType.Number)),
                            ("Y", new JsonSchemaBuilder().Type(SchemaValueType.Number)))
                        .AdditionalProperties(false))))
            .AdditionalProperties(true);
    }
}
