using System.Security.Claims;
using CrestApps.OrchardCore.KnowledgeBase.Indexes;
using CrestApps.OrchardCore.KnowledgeBase.Models;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.KnowledgeBase.Services;

/// <summary>
/// Finds the published articles a user may read: by words in the title or summary, by category, or the latest ones.
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

    private readonly ISession _session;
    private readonly KnowledgeArticleAccess _access;

    public KnowledgeArticleSearch(ISession session, KnowledgeArticleAccess access)
    {
        _session = session;
        _access = access;
    }

    /// <summary>
    /// Finds articles. Each word of <paramref name="terms"/> must be in the title or the summary; the most helpful come
    /// first.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="terms">The words, or none for every article.</param>
    /// <param name="category">The category, or none.</param>
    /// <param name="take">How many to return.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The articles.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleIndex>> FindAsync(ClaimsPrincipal user, string terms, string category, int take, CancellationToken cancellationToken = default)
    {
        var audiences = (await _access.GetAudiencesAsync(user)).ToArray();
        var query = _session.QueryIndex<KnowledgeArticleIndex>(index => index.Audience.IsIn(audiences));

        if (!string.IsNullOrWhiteSpace(category))
        {
            var name = category.Trim();
            query = query.Where(index => index.Category == name);
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
        => SuggestAsync(user, text, take, reader: null, cancellationToken);

    /// <summary>
    /// The articles that match what someone is writing, among those the user may read and, when <paramref name="reader"/>
    /// is given, that reader may read too: an agent answering a customer sees only what the customer can open.
    /// </summary>
    /// <param name="user">The user writing.</param>
    /// <param name="text">What they wrote.</param>
    /// <param name="take">How many at most.</param>
    /// <param name="reader">The audiences the message's reader may read, or <see langword="null"/> for the user's own.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The articles, best first.</returns>
    public async Task<IReadOnlyList<KnowledgeArticleIndex>> SuggestAsync(ClaimsPrincipal user, string text, int take, IReadOnlyCollection<KnowledgeArticleAudience> reader, CancellationToken cancellationToken = default)
    {
        var words = SuggestionWords(text);

        if (words.Count == 0)
        {
            return [];
        }

        var audiences = (await _access.GetAudiencesAsync(user)).Where(audience => reader is null || reader.Contains(audience)).ToArray();

        if (audiences.Length == 0)
        {
            return [];
        }

        var found = new Dictionary<long, (KnowledgeArticleIndex Article, int Hits)>();

        foreach (var word in words)
        {
            foreach (var article in await _session.QueryIndex<KnowledgeArticleIndex>(index => index.Audience.IsIn(audiences) && (index.Title.Contains(word) || index.Summary.Contains(word))).Take(50).ListAsync(cancellationToken))
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
    /// Lists the categories a user can see articles in, with how many each holds.
    /// </summary>
    /// <param name="user">The reader.</param>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>The categories and their counts.</returns>
    public async Task<IReadOnlyList<(string Category, int Count)>> CategoriesAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
    {
        var audiences = (await _access.GetAudiencesAsync(user)).ToArray();
        var rows = await _session.QueryIndex<KnowledgeArticleIndex>(index => index.Audience.IsIn(audiences) && index.Category != null).ListAsync(cancellationToken);

        return [.. rows.GroupBy(row => row.Category, StringComparer.OrdinalIgnoreCase)
            .Select(group => (group.First().Category, group.Count()))
            .OrderBy(pair => pair.Item1, StringComparer.CurrentCultureIgnoreCase)];
    }

    // At most five words of two letters or more, so a search stays cheap.
    internal static IEnumerable<string> Words(string terms)
        => (terms ?? string.Empty)
            .Split([' ', '\t', ',', '.', '?', '!'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(word => word.Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5);
}
