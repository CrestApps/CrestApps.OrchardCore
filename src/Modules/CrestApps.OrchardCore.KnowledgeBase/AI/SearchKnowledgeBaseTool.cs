using System.Security.Claims;
using CrestApps.OrchardCore.ContentFields.Taxonomies;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using CrestApps.OrchardCore.KnowledgeBase.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.AI;

/// <summary>
/// Finds the published help articles the user may read, with the help center's own search: first the articles with every
/// word in the title or summary (the help center's search), then those with some of the longer words (its suggestions,
/// which a question needs), within a category when one is given. With <c>reader</c>, only the articles that reader may
/// open too, as the suggestions a ticket reply offers. Drafts are never searched: the help center's index holds the
/// published articles only.
/// </summary>
internal sealed class SearchKnowledgeBaseTool : KnowledgeBaseAIToolBase
{
    /// <summary>
    /// The tool's name.
    /// </summary>
    public const string TheName = "searchKnowledgeBase";

    /// <summary>
    /// The articles returned when the call does not say.
    /// </summary>
    public const int DefaultTake = 5;

    /// <summary>
    /// The most articles returned.
    /// </summary>
    public const int MaxTake = 10;

    /// <summary>
    /// Initializes a new instance of the <see cref="SearchKnowledgeBaseTool"/> class.
    /// </summary>
    public SearchKnowledgeBaseTool()
        : base("""
            {
              "type": "object",
              "properties": {
                "query": {
                  "type": "string",
                  "description": "Keywords or the question, such as 'reset password' or 'How do I download my invoices?'. Leave it out to list a category's articles, or the most helpful ones."
                },
                "category": {
                  "type": "string",
                  "description": "Only this category and the categories under it: its id, its name or its path, such as 'Billing › Invoices'."
                },
                "reader": {
                  "type": "string",
                  "enum": [ "me", "public", "signedIn" ],
                  "description": "Who will open the links. 'me' (the default) is the user. 'public' is a visitor or a requester without a login: only articles for everyone. 'signedIn' is any signed-in person: articles for everyone and for signed-in people. Set it before putting a link in a reply to a customer."
                },
                "take": {
                  "type": "integer",
                  "minimum": 1,
                  "maximum": 10,
                  "description": "How many articles to return, 1 to 10. Defaults to 5."
                }
              },
              "additionalProperties": false
            }
            """)
    {
    }

    /// <inheritdoc/>
    public override string Name => TheName;

    /// <inheritdoc/>
    public override string Description => "Finds published help articles the user may read, by keywords or a question, optionally within a category, best match first: each article's title, summary, category, link and the passage of its body that matches. Set reader before putting a link in a reply to a customer, so only articles they can open come back. Drafts are not searched; list them with listContentItems.";

    /// <inheritdoc/>
    protected override async Task<object> InvokeAsync(AIFunctionArguments arguments, IServiceProvider services, ClaimsPrincipal caller, CancellationToken cancellationToken)
    {
        var query = ReadString(arguments, "query");
        var categoryText = ReadString(arguments, "category");
        var take = Math.Clamp(ReadInt(arguments, "take") ?? DefaultTake, 1, MaxTake);

        if (!TryGetReader(ReadString(arguments, "reader"), out var reader, out var readerRoles))
        {
            return "Nothing was done: reader must be 'me', 'public' or 'signedIn'.";
        }

        var tree = await services.GetRequiredService<KnowledgeBaseCategories>().GetTreeAsync();
        TaxonomyTermNode category = null;
        IReadOnlyCollection<string> categoryIds = null;

        if (categoryText is not null)
        {
            var matches = FindCategories(tree, categoryText);

            if (matches.Count != 1)
            {
                return Json(new
                {
                    message = matches.Count == 0
                        ? $"Nothing was searched: no category is named '{categoryText}'. Give one of these by its id or its path."
                        : $"Nothing was searched: several categories are named '{categoryText}'. Give one of these by its id or its path.",
                    categories = matches.Count == 0
                        ? await ListCategoriesAsync(services, caller, tree, cancellationToken)
                        : matches.Select(node => (object)new { id = node.Id, name = tree.GetPathText(node.Id, KnowledgeArticleProjection.PathSeparator) }),
                });
            }

            category = matches[0];
            categoryIds = KnowledgeBaseCategories.WithDescendants(tree, category.Id);
        }

        // One more than asked for, to know whether more match.
        var search = services.GetRequiredService<KnowledgeArticleSearch>();
        var found = await search.FindAsync(caller, query, categoryIds, take + 1, readerRoles, cancellationToken);
        IReadOnlyList<KnowledgeArticleIndex> suggested = [];

        // A question rarely has every word in an article's title or summary: the suggestions match some of its longer words.
        if (query is not null && found.Count <= take)
        {
            var some = await search.SuggestAsync(caller, query, take + 1, readerRoles, cancellationToken);

            suggested = categoryIds is null
                ? some
                : [.. some.Where(article => article.CategoryId is not null && categoryIds.Contains(article.CategoryId))];
        }

        var merged = KnowledgeBaseHelpCenter.Merge(null, found, suggested, take + 1);
        var allWords = found.Select(article => article.ContentItemId).ToHashSet(StringComparer.Ordinal);
        var items = await ProjectAsync(services, caller, merged.Take(take), allWords, query, tree);

        if (items.Count == 0)
        {
            return Json(new
            {
                query,
                category = category is null ? null : KnowledgeArticleProjection.GetCategory(tree, category.Id),
                reader,
                items,
                returned = 0,
                truncated = false,
                message = (reader == "me"
                    ? "No published article the user may read matches."
                    : "No published article that both the user and that reader may read matches.")
                    + " Try fewer or other keywords, or another category.",
                categories = await ListCategoriesAsync(services, caller, tree, cancellationToken),
            });
        }

        return Json(new
        {
            query,
            category = category is null ? null : KnowledgeArticleProjection.GetCategory(tree, category.Id),
            reader,
            items,
            returned = items.Count,

            // More may match: the search found more than were returned.
            truncated = merged.Count > take,
            dataNote = DataNote,
        });
    }

    /// <summary>
    /// Reads who will open the links.
    /// </summary>
    /// <param name="value">The argument.</param>
    /// <param name="reader">The reader's name, as the result echoes it.</param>
    /// <param name="readerRoles">The reader's roles, or <see langword="null"/> for the user's own.</param>
    /// <returns><see langword="false"/> for a value the schema does not offer.</returns>
    internal static bool TryGetReader(string value, out string reader, out IReadOnlyCollection<string> readerRoles)
    {
        switch (value?.ToLowerInvariant())
        {
            case null or "me":
                reader = "me";
                readerRoles = null;
                return true;
            case "public":
                reader = "public";
                readerRoles = KnowledgeArticleSearch.VisitorReader;
                return true;
            case "signedin":
                reader = "signedIn";
                readerRoles = KnowledgeArticleSearch.SignedInReader;
                return true;
            default:
                reader = null;
                readerRoles = null;
                return false;
        }
    }

    /// <summary>
    /// Finds the categories a text names: a term's id, or its name or path (any of " › ", " > " or "/" between the
    /// names), without regard to case.
    /// </summary>
    /// <param name="tree">The categories.</param>
    /// <param name="text">The text.</param>
    /// <returns>The categories it names.</returns>
    internal static List<TaxonomyTermNode> FindCategories(TaxonomyTermTree tree, string text)
    {
        if (tree.Find(text) is { } byId)
        {
            return [byId];
        }

        var names = Split(text);

        return [.. tree.Nodes.Where(node => names.Count == 1
            ? string.Equals(node.Name?.Trim(), names[0], StringComparison.OrdinalIgnoreCase)
            : Split(tree.GetPathText(node.Id, "\u0001")).SequenceEqual(names, StringComparer.OrdinalIgnoreCase))];
    }

    private static List<string> Split(string path)
        => [.. (path ?? string.Empty).Split(['›', '>', '/', '\u0001'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)];

    // The categories the user may read articles in, with their counts and paths, for the model to pick from.
    private static async Task<IEnumerable<object>> ListCategoriesAsync(IServiceProvider services, ClaimsPrincipal caller, TaxonomyTermTree tree, CancellationToken cancellationToken)
        => (await services.GetRequiredService<KnowledgeBaseHelpCenter>().GetCategoriesAsync(caller, cancellationToken))
            .Select(category => (object)new
            {
                id = category.Id,
                name = tree.GetPathText(category.Id, KnowledgeArticleProjection.PathSeparator) ?? category.Name,
                articles = category.Count,
            });

    // Loads the published articles, as the help center's suggestions do, keeps those the user may open and returns the
    // allow-listed fields of each, in the search's order.
    private static async Task<List<object>> ProjectAsync(IServiceProvider services, ClaimsPrincipal caller, IEnumerable<KnowledgeArticleIndex> articles, HashSet<string> allWords, string query, TaxonomyTermTree tree)
    {
        var ordered = articles.Select(article => article.ContentItemId).ToList();
        var items = new List<object>();

        if (ordered.Count == 0)
        {
            return items;
        }

        var loaded = new Dictionary<string, ContentItem>(StringComparer.Ordinal);

        foreach (var item in await services.GetRequiredService<IContentManager>().GetAsync(ordered, VersionOptions.Published))
        {
            loaded.TryAdd(item.ContentItemId, item);
        }

        // The most telling words first: the longer ones the suggestions match, then the searched ones.
        var words = query is null
            ? []
            : KnowledgeArticleSearch.SuggestionWords(query)
                .Concat(KnowledgeArticleSearch.Words(query).Where(word => word.Length >= 3))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        var linkBase = await KnowledgeArticleProjection.GetLinkBaseAsync(services);

        foreach (var id in ordered)
        {
            if (!loaded.TryGetValue(id, out var item)
                || !item.TryGet<KnowledgeArticlePart>(out var part)
                || !await CanViewAsync(services, caller, item))
            {
                continue;
            }

            items.Add(new
            {
                id = item.ContentItemId,
                title = item.DisplayText,
                summary = part.Summary,
                category = KnowledgeArticleProjection.GetCategory(tree, KnowledgeArticleIndexProvider.CategoryOf(item)),
                link = KnowledgeArticleProjection.GetLink(services, linkBase, item),
                passage = KnowledgeArticleProjection.GetPassage(KnowledgeArticleProjection.GetBody(item), words),
                publishedAt = await ToLocalAsync(services, item.PublishedUtc),
                helpfulCount = part.HelpfulCount,
                notHelpfulCount = part.NotHelpfulCount,
                match = query is null ? "browse" : allWords.Contains(id) ? "allWords" : "someWords",
            });
        }

        return items;
    }
}
