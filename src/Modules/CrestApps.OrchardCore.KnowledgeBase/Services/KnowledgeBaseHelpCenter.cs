using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.ViewModels;
using OrchardCore.ContentManagement;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// What the help center's page and its widgets show alike: the categories with how many articles the reader may open in
/// each, and an article's related articles. Everything keeps to what the reader may open (Content Access Control).
/// </summary>
public sealed class KnowledgeBaseHelpCenter
{
    /// <summary>
    /// The articles the home page shows under each category; the category's page lists them all.
    /// </summary>
    public const int ArticlesPerGroup = 5;

    private readonly KnowledgeArticleSearch _search;
    private readonly KnowledgeBaseCategories _categories;

    private IReadOnlyDictionary<string, (int Count, IReadOnlyList<KnowledgeArticleIndex> Articles)> _grouped;

    /// <summary>
    /// Initializes a new instance of the <see cref="KnowledgeBaseHelpCenter"/> class.
    /// </summary>
    public KnowledgeBaseHelpCenter(KnowledgeArticleSearch search, KnowledgeBaseCategories categories)
    {
        _search = search;
        _categories = categories;
    }

    /// <summary>
    /// Gets each category's articles the reader may open, read once per request.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Each category's count and most helpful articles.</returns>
    public async Task<IReadOnlyDictionary<string, (int Count, IReadOnlyList<KnowledgeArticleIndex> Articles)>> GetGroupedAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        => _grouped ??= await _search.ByCategoryAsync(user, ArticlesPerGroup, cancellationToken);

    /// <summary>
    /// Gets the categories with articles the reader may open, in the taxonomy's order. A category counts the articles of
    /// the categories under it too, as choosing it lists them.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The categories.</returns>
    public async Task<IReadOnlyList<HelpCenterCategory>> GetCategoriesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var tree = await _categories.GetTreeAsync();
        var grouped = await GetGroupedAsync(user, cancellationToken);

        return [.. tree.Nodes
            .Select(node => new HelpCenterCategory(node.Id, node.Name, node.Level, KnowledgeBaseCategories.WithDescendants(tree, node.Id).Sum(id => grouped.TryGetValue(id, out var group) ? group.Count : 0)))
            .Where(category => category.Count > 0)];
    }

    /// <summary>
    /// Gets an article's related articles: those of its category the reader may open first, most helpful first, then those
    /// whose title or summary share the words of its title and summary; never the article itself.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="article">The article.</param>
    /// <param name="count">How many at most.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The related articles.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleIndex>> GetRelatedAsync(ClaimsPrincipal user, ContentItem article, int count, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(article);

        var take = Math.Clamp(count, 1, Models.RelatedArticlesPart.MaxCount);
        var categoryId = KnowledgeArticleIndexProvider.CategoryOf(article);

        // One more than needed, as the article itself may be among them.
        var sameCategory = categoryId is null ? [] : await _search.FindAsync(user, null, [categoryId], take + 1, cancellationToken);
        var text = article.DisplayText + " " + article.GetOrCreate<Models.KnowledgeArticlePart>().Summary;
        var similar = await _search.SuggestAsync(user, text, take + 1, cancellationToken);

        return Merge(article.ContentItemId, sameCategory, similar, take);
    }

    /// <summary>
    /// Merges the articles of the same category and the similar ones: the first ones first, without the article itself or
    /// an article twice.
    /// </summary>
    /// <param name="contentItemId">The article itself.</param>
    /// <param name="sameCategory">The articles of its category.</param>
    /// <param name="similar">The articles that share its words.</param>
    /// <param name="take">How many at most.</param>
    /// <returns>The related articles.</returns>
    internal static IReadOnlyList<KnowledgeArticleIndex> Merge(string contentItemId, IEnumerable<KnowledgeArticleIndex> sameCategory, IEnumerable<KnowledgeArticleIndex> similar, int take)
        => [.. sameCategory.Concat(similar)
            .Where(index => !string.Equals(index.ContentItemId, contentItemId, StringComparison.Ordinal))
            .DistinctBy(index => index.ContentItemId, StringComparer.Ordinal)
            .Take(take)];
}
