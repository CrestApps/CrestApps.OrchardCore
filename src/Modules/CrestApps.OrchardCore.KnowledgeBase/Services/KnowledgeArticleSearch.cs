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
