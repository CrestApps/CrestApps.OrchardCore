using CrestApps.OrchardCore.Recipes.Core.Schemas.Steps;
using Json.Schema;

namespace CrestApps.OrchardCore.Recipes.Core.Schemas.Sitemaps.Sources;

/// <summary>
/// Describes the recipe schema for the <c>ContentTypesSitemapSource</c> sitemap source.
/// </summary>
public sealed class ContentTypesSitemapSourceSchema : SitemapSourceSchemaDefinitionBase
{
    /// <inheritdoc />
    public override string Name { get; } = "ContentTypesSitemapSource";

    /// <inheritdoc />
    public override string TypeDiscriminator { get; } = "OrchardCore.Sitemaps.Models.ContentTypesSitemapSource, OrchardCore.Sitemaps.Abstractions";

    /// <inheritdoc />
    protected override string DisplayText => "Content Types";

    /// <inheritdoc />
    protected override string Description => "Adds content items to the sitemap, either every indexable content type or a selected list.";

    /// <inheritdoc />
    protected override IEnumerable<(string Name, JsonSchemaBuilder Schema)> GetPropertyDefinitions(SitemapSourceSchemaContext context)
    {
        var contentTypeNames = context.Examples.ContentTypeNames;

        yield return ("IndexAll", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("When true, every indexable content type is added to the sitemap. When false, only the content types listed in 'ContentTypes' are added."));

        yield return ("LimitItems", new JsonSchemaBuilder()
            .Type(SchemaValueType.Boolean)
            .Description("When true, the sitemap is limited to a single content type described by 'LimitedContentType'."));

        yield return ("ChangeFrequency", SitemapSchemaBuilders.ChangeFrequency("The default change frequency reported for the source's entries."));

        yield return ("Priority", SitemapSchemaBuilders.Priority("The default priority reported for the source's entries, from 0 to 10, where 10 maps to the highest sitemap priority of 1.0."));

        yield return ("ContentTypes", new JsonSchemaBuilder()
            .Type(SchemaValueType.Array)
            .Items(BuildEntrySchema(contentTypeNames))
            .Description("The content types to add when 'IndexAll' is false."));

        yield return ("LimitedContentType", BuildLimitedEntrySchema(contentTypeNames));
    }

    private static JsonSchemaBuilder BuildEntrySchema(IReadOnlyList<string> contentTypeNames)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentTypeName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(contentTypeNames)
                    .Description("The technical name of the content type to add.")),
                ("ChangeFrequency", SitemapSchemaBuilders.ChangeFrequency("The change frequency reported for this content type's entries.")),
                ("Priority", SitemapSchemaBuilders.Priority("The priority reported for this content type's entries, from 0 to 10.")))
            .AdditionalProperties(true)
            .Description("A content type entry contributed to the sitemap.");

    private static JsonSchemaBuilder BuildLimitedEntrySchema(IReadOnlyList<string> contentTypeNames)
        => new JsonSchemaBuilder()
            .Type(SchemaValueType.Object)
            .Properties(
                ("ContentTypeName", new JsonSchemaBuilder()
                    .Type(SchemaValueType.String)
                    .WithSuggestions(contentTypeNames)
                    .Description("The technical name of the single content type indexed when 'LimitItems' is true.")),
                ("ChangeFrequency", SitemapSchemaBuilders.ChangeFrequency("The change frequency reported for the limited content type's entries.")),
                ("Priority", SitemapSchemaBuilders.Priority("The priority reported for the limited content type's entries, from 0 to 10.")),
                ("Skip", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(0)
                    .Description("The number of content items to skip before adding entries.")),
                ("Take", new JsonSchemaBuilder()
                    .Type(SchemaValueType.Integer)
                    .Minimum(0)
                    .Description("The maximum number of content items to add. A sitemap file supports up to 50,000 entries.")))
            .AdditionalProperties(true)
            .Description("The single content type indexed when 'LimitItems' is true.");
}
