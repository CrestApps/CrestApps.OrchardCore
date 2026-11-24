using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads responses through the response index.
/// </summary>
public sealed class SurveyResponseQueries : ISurveyResponseQueries
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyResponseQueries"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyResponseQueries(ISession session)
    {
        _session = session;
    }

    /// <inheritdoc />
    public async Task<SurveyResponse> FindByIdAsync(string responseId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(responseId))
        {
            return null;
        }

        return await _session.Query<SurveyResponse, SurveyResponseIndex>(index => index.ItemId == responseId, collection: SurveysConstants.ResponsesCollectionName)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<(int Count, IReadOnlyList<SurveyResponse> Items)> ListAsync(SurveyResponseQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var take = Math.Clamp(query.Take, 1, SurveysConstants.PageSize);
        var skip = Math.Max(0, query.Skip);
        var named = query.SurveyIds.Count > 0;

        // Surveys left out of named surveys are simply not named, so no NOT IN list is needed. Left out of every survey,
        // they go in one NOT IN list, which must fit under SQL Server's parameter limit like an IN list: a caller with more
        // names the surveys it reads instead.
        var excluded = new HashSet<string>(query.ExcludedSurveyIds.Where(id => !string.IsNullOrEmpty(id)), StringComparer.Ordinal);

        if (!named && !SurveyIdChunks.FitsOneList(excluded.Count))
        {
            throw new ArgumentException($"At most {SurveyIdChunks.Size} surveys can be left out of every survey; name the surveys to read instead.", nameof(query));
        }

        var chunks = SurveyIdChunks.Of(query.SurveyIds.Where(id => !excluded.Contains(id))).ToArray();

        if (named && chunks.Length == 0)
        {
            return (0, []);
        }

        if (!named || chunks.Length <= 1)
        {
            var q = Filter(query, named ? chunks[0] : null, named ? [] : [.. excluded]);
            var count = await q.CountAsync(cancellationToken);
            var items = await Order(q, query).Skip(skip).Take(take).ListAsync(cancellationToken);

            return (count, items.ToList());
        }

        // More surveys than one IN list holds (SQL Server refuses more than 2,100 parameters): each chunk is counted and
        // its first skip + take responses read in the page's order; the page is cut from those, merged in the same order
        // the database uses, ties included (Order), so a page never repeats or skips a response.
        var total = 0;
        var merged = new List<SurveyResponse>();

        foreach (var chunk in chunks)
        {
            var q = Filter(query, chunk, []);
            total += await q.CountAsync(cancellationToken);
            merged.AddRange(await Order(q, query).Take(skip + take).ListAsync(cancellationToken));
        }

        IEnumerable<SurveyResponse> ordered = query.OrderById
            ? merged.OrderBy(response => response.ItemId, StringComparer.Ordinal)
            : query.OldestFirst
            ? merged.OrderBy(response => response.CompletedUtc ?? DateTime.MinValue).ThenBy(response => response.StartedUtc).ThenBy(response => response.ItemId, StringComparer.Ordinal)
            : merged.OrderByDescending(response => response.StartedUtc).ThenByDescending(response => response.ItemId, StringComparer.Ordinal);

        return (total, ordered.Skip(skip).Take(take).ToList());
    }

    /// <inheritdoc />
    public async Task<int> CountForSurveyAsync(string surveyId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(surveyId))
        {
            return 0;
        }

        return await _session.QueryIndex<SurveyResponseIndex>(index => index.SurveyId == surveyId && !index.IsTest, collection: SurveysConstants.ResponsesCollectionName)
            .CountAsync(cancellationToken);
    }

    // Ties are broken by the response id, which the merge of several chunks can compare too (the index row id is not on
    // the response, and a save gives a response a new one).
    private static IQuery<SurveyResponse, SurveyResponseIndex> Order(IQuery<SurveyResponse, SurveyResponseIndex> query, SurveyResponseQuery options)
        => options.OrderById
            ? query.OrderBy(index => index.ItemId)
            : options.OldestFirst
            ? query.OrderBy(index => index.CompletedUtc).ThenBy(index => index.StartedUtc).ThenBy(index => index.ItemId)
            : query.OrderByDescending(index => index.StartedUtc).ThenByDescending(index => index.ItemId);

    // Applies the query's filters; surveyIds is one chunk of its surveys, or null for every survey but the excluded ones.
    private IQuery<SurveyResponse, SurveyResponseIndex> Filter(SurveyResponseQuery query, string[] surveyIds, string[] excludedSurveys)
    {
        var q = _session.Query<SurveyResponse, SurveyResponseIndex>(collection: SurveysConstants.ResponsesCollectionName);

        if (surveyIds is not null)
        {
            q = q.Where(index => index.SurveyId.IsIn(surveyIds));
        }

        if (!string.IsNullOrEmpty(query.UserId))
        {
            var userId = query.UserId;
            q = q.Where(index => index.UserId == userId);
        }

        if (!string.IsNullOrEmpty(query.ContextRef))
        {
            var contextRef = query.ContextRef;
            q = q.Where(index => index.ContextRef == contextRef);
        }

        if (query.ContextKinds.Count > 0)
        {
            var kinds = query.ContextKinds.ToArray();
            q = q.Where(index => index.ContextKind.IsIn(kinds));
        }

        if (query.ExcludedContextKinds.Count > 0)
        {
            var excluded = query.ExcludedContextKinds.ToArray();
            q = q.Where(index => index.ContextKind == null || index.ContextKind.IsNotIn(excluded));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.Select(status => status.ToString()).ToArray();
            q = q.Where(index => index.Status.IsIn(statuses));
        }

        if (query.ExcludedStatuses.Count > 0)
        {
            var excludedStatuses = query.ExcludedStatuses.Select(status => status.ToString()).ToArray();
            q = q.Where(index => index.Status.IsNotIn(excludedStatuses));
        }

        if (excludedSurveys.Length > 0)
        {
            q = q.Where(index => index.SurveyId.IsNotIn(excludedSurveys));
        }

        if (!string.IsNullOrEmpty(query.ExcludedUserId))
        {
            var excludedUser = query.ExcludedUserId;
            q = q.Where(index => index.UserId == null || index.UserId != excludedUser);
        }
        if (query.Grading is { } grading)
        {
            var value = grading.ToString();
            q = q.Where(index => index.Grading == value);
        }

        if (query.VersionNumber is { } number)
        {
            q = q.Where(index => index.VersionNumber == number);
        }

        if (!string.IsNullOrEmpty(query.CollectorId))
        {
            var collectorId = query.CollectorId;
            q = q.Where(index => index.CollectorId == collectorId);
        }

        if (!query.IncludeTest)
        {
            q = q.Where(index => !index.IsTest);
        }

        if (query.StartedFromUtc is { } from)
        {
            q = q.Where(index => index.StartedUtc >= from);
        }

        if (query.StartedToUtc is { } to)
        {
            q = q.Where(index => index.StartedUtc < to);
        }

        if (query.CompletedFromUtc is { } completedFrom)
        {
            q = q.Where(index => index.CompletedUtc >= completedFrom);
        }

        if (query.CompletedToUtc is { } completedTo)
        {
            q = q.Where(index => index.CompletedUtc < completedTo);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(index => index.Number.Contains(search));
        }

        // The groups of an anonymous survey too small to show leave the query itself, so the count and the pages leave
        // them out too.
        return q.Excluding(query.HiddenGroups);
    }
}
