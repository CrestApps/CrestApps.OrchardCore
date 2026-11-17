using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.Roles.Core;
using CrestApps.OrchardCore.Roles.Core.Indexes;
using OrchardCore;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Finds the published articles a user may read: by words in the title or summary, by category, or the latest ones.
/// Who may read an article is Content Access Control's: each query keeps, in SQL, to the articles whose
/// <see cref="ContentAccessRoleIndex"/> rows meet the reader's roles.
/// </summary>
public sealed class KnowledgeArticleSearch
{
    // Words too common to tell articles apart, left out of suggestions.
    private static readonly HashSet<string> _commonWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "about", "also", "been", "could", "does", "from", "have", "hello", "help", "here", "into", "just", "like",
        "more", "need", "please", "some", "than", "thank", "thanks", "that", "their", "them", "then", "there",
        "they", "this", "tried", "want", "were", "what", "when", "where", "which", "will", "with", "would", "your",
    };

    // The most articles the home page groups by category; a help center holds far fewer.
    private const int MaxGroupedArticles = 1000;

    /// <summary>
    /// The roles of a reader who is not signed in, for <see cref="SuggestAsync(ClaimsPrincipal, string, int, IReadOnlyCollection{string}, CancellationToken)"/>.
    /// </summary>
    public static readonly IReadOnlyCollection<string> VisitorReader = [OrchardCoreConstants.Roles.Anonymous];

    /// <summary>
    /// The roles of a reader who is signed in, with no role known to matter.
    /// </summary>
    public static readonly IReadOnlyCollection<string> SignedInReader = [OrchardCoreConstants.Roles.Authenticated];

    private readonly ISession _session;

    public KnowledgeArticleSearch(ISession session)
    {
        _session = session;
    }

    /// <summary>
    /// Finds articles. Each word of <paramref name="terms"/> must be in the title or the summary; the most helpful come
    /// first.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="terms">The words, or none for every article.</param>
    /// <param name="categoryIds">The categories (a category and those under it), or none for every category.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The articles.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleIndex>> FindAsync(ClaimsPrincipal user, string terms, IReadOnlyCollection<string> categoryIds, int take, CancellationToken cancellationToken = default)
    {
        var query = Readable(user, readerRoles: null);

        if (categoryIds is { Count: > 0 })
        {
            var ids = categoryIds.ToArray();
            query = query.Where(index => index.CategoryId.IsIn(ids));
        }

        foreach (var word in Words(terms))
        {
            query = query.Where(index => index.Title.Contains(word) || index.Summary.Contains(word));
        }

        return [.. await query.OrderByDescending(index => index.HelpfulCount).ThenByDescending(index => index.PublishedUtc).Take(Math.Clamp(take, 1, 100)).ListAsync(cancellationToken)];
    }

    /// <summary>
    /// Suggests articles for something being written: those with any of its longer words (four letters or more, the
    /// last eight) in the title or the summary, the ones matching the most words first, then the most helpful.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="text">What is being written.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The articles.</returns>
    public Task<IReadOnlyList<KnowledgeArticleIndex>> SuggestAsync(ClaimsPrincipal user, string text, int take, CancellationToken cancellationToken = default)
        => SuggestAsync(user, text, take, readerRoles: null, cancellationToken);

    /// <summary>
    /// The articles that match what someone is writing, among those the user may read and, when <paramref name="readerRoles"/>
    /// is given, that reader may read too: an agent answering a customer sees only what the customer can open.
    /// </summary>
    /// <param name="user">The user writing.</param>
    /// <param name="text">What they wrote.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="readerRoles">The roles of the message's reader (<see cref="VisitorReader"/>, <see cref="SignedInReader"/>),
    /// or <see langword="null"/> for the user's own.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The articles, best first.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleIndex>> SuggestAsync(ClaimsPrincipal user, string text, int take, IReadOnlyCollection<string> readerRoles, CancellationToken cancellationToken = default)
    {
        var words = SuggestionWords(text);

        if (words.Count == 0)
        {
            return [];
        }

        var found = new Dictionary<long, (KnowledgeArticleIndex Article, int Hits)>();

        foreach (var word in words)
        {
            foreach (var article in await Readable(user, readerRoles).Where(index => index.Title.Contains(word) || index.Summary.Contains(word)).Take(50).ListAsync(cancellationToken))
            {
                found[article.Id] = found.TryGetValue(article.Id, out var known) ? (known.Article, known.Hits + 1) : (article, 1);
            }
        }

        return [.. found.Values
            .OrderByDescending(pair => pair.Hits)
            .ThenByDescending(pair => pair.Article.HelpfulCount)
            .Take(Math.Clamp(take, 1, 20))
            .Select(pair => pair.Article)];
    }

    internal static List<string> SuggestionWords(string text)
        => [.. (text ?? string.Empty)
            .Split([' ', '\t', '\r', '\n', ',', '.', '?', '!', ';', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 4 && !_commonWords.Contains(word))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .TakeLast(8)];

    /// <summary>
    /// Lists the articles a user may read, by category: for each category with articles, how many it holds and its most
    /// helpful ones. Articles without a category are left out.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="perCategory">How many articles to return for each category.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>Each category's id, its article count and its most helpful articles.</returns>
    public async Task<IReadOnlyDictionary<string, (int Count, IReadOnlyList<KnowledgeArticleIndex> Articles)>> ByCategoryAsync(ClaimsPrincipal user, int perCategory, CancellationToken cancellationToken = default)
    {
        var rows = await Readable(user, readerRoles: null).Where(index => index.CategoryId != null)
            .OrderByDescending(index => index.HelpfulCount)
            .ThenByDescending(index => index.PublishedUtc)
            .Take(MaxGroupedArticles)
            .ListAsync(cancellationToken);

        return rows.GroupBy(row => row.CategoryId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => (group.Count(), (IReadOnlyList<KnowledgeArticleIndex>)[.. group.Take(Math.Clamp(perCategory, 1, 50))]),
                StringComparer.Ordinal);
    }

    // The published articles the user may read and, when given, the reader too. An article with no role picked has an
    // Anonymous and an Authenticated row, so everyone's roles meet it.
    private IQueryIndex<KnowledgeArticleIndex> Readable(ClaimsPrincipal user, IReadOnlyCollection<string> readerRoles)
    {
        var query = _session.QueryIndex<KnowledgeArticleIndex>();

        if (!ContentAccessRoles.SeesEverything(user))
        {
            var roles = ContentAccessRoles.GetReaderRoles(user).ToArray();
            query = query.Where(index => index.ContentItemId.IsIn<ContentAccessRoleIndex>(row => row.ContentItemId, row => row.RoleName.IsIn(roles)));
        }

        if (readerRoles is not null)
        {
            var reader = readerRoles.ToArray();
            query = query.Where(index => index.ContentItemId.IsIn<ContentAccessRoleIndex>(row => row.ContentItemId, row => row.RoleName.IsIn(reader)));
        }

        return query;
    }

    // At most five words of two letters or more, so a search stays cheap.
    internal static IEnumerable<string> Words(string terms)
        => (terms ?? string.Empty)
            .Split([' ', '\t', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);
}
