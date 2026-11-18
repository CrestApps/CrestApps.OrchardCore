using System.Text.Json.Nodes;
using CrestApps.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OrchardCore.ContentManagement;
using OrchardCore.ContentManagement.Records;
using OrchardCore.Taxonomies.Models;
using OrchardCore.Title.Models;
using YesSql;

namespace CrestApps.OrchardCore.KnowledgeBase.Migrations;

/// <summary>
/// Makes the Knowledge Base Categories taxonomy and turns the categories articles had as text into its terms.
/// </summary>
/// <remarks>
/// Articles used to keep their category as text on <c>KnowledgeArticlePart</c>. Each distinct text (compared without
/// case) becomes a top term of the taxonomy, or finds the top term of that name already there, and every version of an
/// article that has the text but no category yet gets that term in its category field; the text goes. Running it again
/// finds nothing left to convert. It runs after the migration's transaction is committed, through the content manager
/// only to create the taxonomy itself; the terms and articles are saved through the session, so no new version is made.
/// </remarks>
internal static class KnowledgeCategoryTerms
{
    /// <summary>
    /// The name of the taxonomy the feature creates.
    /// </summary>
    public const string TaxonomyName = "Knowledge Base Categories";

    private const string LegacyCategoryProperty = "Category";

    /// <summary>
    /// Creates the taxonomy when it isn't there and converts the text categories, after the migration.
    /// </summary>
    /// <param name="services">The deferred task's services.</param>
    /// <param name="taxonomyId">The taxonomy the category field uses.</param>
    /// <returns>A task.</returns>
    public static async Task CreateAndConvertAsync(IServiceProvider services, string taxonomyId)
    {
        var contentManager = services.GetRequiredService<IContentManager>();
        var session = services.GetRequiredService<ISession>();
        var taxonomy = await contentManager.GetAsync(taxonomyId, VersionOptions.Latest);

        if (taxonomy is null)
        {
            taxonomy = await contentManager.NewAsync("Taxonomy");
            taxonomy.ContentItemId = taxonomyId;
            taxonomy.DisplayText = TaxonomyName;
            taxonomy.Alter<TitlePart>(part => part.Title = TaxonomyName);
            taxonomy.Alter<TaxonomyPart>(part => part.TermContentType = KnowledgeBaseConstants.CategoryTermType);
            await contentManager.CreateAsync(taxonomy, VersionOptions.Published);
        }

        var converted = await ConvertAsync(session, taxonomy);

        var logger = services.GetRequiredService<ILogger<KnowledgeBaseMigrations>>();

        if (converted > 0 && logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("{Count} knowledge article versions had their text category turned into a term of the categories' taxonomy.", converted);
        }
    }

    /// <summary>
    /// Turns the text categories of the articles into terms of the taxonomy and sets them on the articles.
    /// </summary>
    /// <param name="session">The session the terms and the articles are saved in.</param>
    /// <param name="taxonomy">The taxonomy (its latest version, which is its published one: taxonomies aren't drafted).</param>
    /// <returns>How many article versions were converted.</returns>
    public static async Task<int> ConvertAsync(ISession session, ContentItem taxonomy)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(taxonomy);

        var articles = await session.Query<ContentItem, ContentItemIndex>(index => index.ContentType == KnowledgeBaseConstants.ArticleType && (index.Latest || index.Published))
            .ListAsync();
        var terms = TermsOf(taxonomy);
        var byName = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var term in terms.OfType<JsonObject>())
        {
            if (term["DisplayText"]?.GetValue<string>() is { Length: > 0 } name && term["ContentItemId"]?.GetValue<string>() is { Length: > 0 } id)
            {
                byName.TryAdd(name.Trim(), id);
            }
        }

        var converted = 0;
        var termsAdded = false;

        foreach (var article in articles)
        {
            var content = (JsonObject)article.Content;

            if (content[KnowledgeBaseConstants.PartName] is not JsonObject part || !part.ContainsKey(LegacyCategoryProperty))
            {
                continue;
            }

            var text = part[LegacyCategoryProperty]?.GetValue<string>()?.Trim();

            part.Remove(LegacyCategoryProperty);

            if (!string.IsNullOrEmpty(text) && Indexes.KnowledgeArticleIndexProvider.CategoryOf(article) is null)
            {
                if (!byName.TryGetValue(text, out var termId))
                {
                    termId = UniqueId.GenerateId();
                    byName[text] = termId;
                    terms.Add(Term(termId, text));
                    termsAdded = true;
                }

                var typePart = content[article.ContentType] as JsonObject ?? [];

                typePart[KnowledgeBaseConstants.CategoryField] = new JsonObject
                {
                    ["TaxonomyContentItemId"] = taxonomy.ContentItemId,
                    ["TermContentItemIds"] = new JsonArray(termId),
                };
                content[article.ContentType] = typePart;
            }

            await session.SaveAsync(article);
            converted++;
        }

        if (termsAdded)
        {
            await session.SaveAsync(taxonomy);
        }

        return converted;
    }

    private static JsonArray TermsOf(ContentItem taxonomy)
    {
        var content = (JsonObject)taxonomy.Content;
        var part = content["TaxonomyPart"] as JsonObject ?? [];

        content["TaxonomyPart"] = part;

        if (part["Terms"] is not JsonArray terms)
        {
            terms = [];
            part["Terms"] = terms;
        }

        part["TermContentType"] ??= KnowledgeBaseConstants.CategoryTermType;

        return terms;
    }

    // A term as a taxonomy keeps it: a content item of the term type, with its title.
    private static JsonObject Term(string id, string name)
        => new()
        {
            ["ContentItemId"] = id,
            ["ContentItemVersionId"] = UniqueId.GenerateId(),
            ["ContentType"] = KnowledgeBaseConstants.CategoryTermType,
            ["DisplayText"] = name,
            ["Latest"] = true,
            ["Published"] = true,
            ["TitlePart"] = new JsonObject { ["Title"] = name },
        };
}
