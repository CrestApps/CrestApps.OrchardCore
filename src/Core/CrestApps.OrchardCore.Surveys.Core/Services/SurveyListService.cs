using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Dapper;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using YesSql;
using YesSql.Services;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads pages of the survey list with its filters.
/// </summary>
public sealed class SurveyListService
{
    private readonly ISession _session;
    private readonly SurveyIndexReader _reader;
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyListService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    /// <param name="reader">Reads the surveys whose index rows wait for the reindex step.</param>
    /// <param name="logger">The logger, or <see langword="null"/>.</param>
    public SurveyListService(ISession session, SurveyIndexReader reader, ILogger<SurveyListService> logger = null)
    {
        _session = session;
        _reader = reader;
        _logger = (ILogger)logger ?? NullLogger.Instance;
    }

    /// <summary>
    /// The most tags the tag filter lists.
    /// </summary>
    public const int MaxTags = 500;

    /// <summary>
    /// Reads a page of surveys, last changed first. Every filter and the page are applied in the database.
    /// </summary>
    /// <param name="query">The filters.</param>
    /// <param name="skip">How many to skip.</param>
    /// <param name="take">How many to read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The total count and the page.</returns>
    public async Task<(int Count, IReadOnlyList<Survey> Items)> ListAsync(SurveyListQuery query, int skip, int take, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        IQuery<Survey> root = _session.Query<Survey>(collection: SurveysConstants.CollectionName);

        // Rows indexed before the index held the owner and the tags are read from their surveys until the reindex step
        // reaches them, so a filter on either never drops a survey in the meantime.
        var stale = !string.IsNullOrWhiteSpace(query.Tag) || !string.IsNullOrEmpty(query.OwnerId)
            ? await _reader.ListNotReindexedAsync(cancellationToken)
            : [];
        string[] tagged = null;

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = SurveyTagIndexProvider.Normalize(query.Tag);
            var staleTagged = stale
                .Where(survey => (survey.Tags ?? []).Any(candidate => !string.IsNullOrWhiteSpace(candidate) && SurveyTagIndexProvider.Normalize(candidate) == tag))
                .Select(survey => survey.ItemId)
                .ToList();

            if (staleTagged.Count > 0)
            {
                var indexed = (await _session.QueryIndex<SurveyTagIndex>(index => index.Tag == tag, collection: SurveysConstants.CollectionName).ListAsync(cancellationToken))
                    .Select(row => row.SurveyId);

                tagged = [.. indexed.Concat(staleTagged).Where(id => !string.IsNullOrEmpty(id)).Distinct(StringComparer.Ordinal)];

                if (!SurveyIdChunks.FitsOneList(tagged.Length))
                {
                    // One IN list holds at most SurveyIdChunks.Size ids, so the tag index alone pages the surveys: every
                    // indexed survey is listed, and those still waiting for the reindex step join the filter once it
                    // reaches them. This is said in the log rather than cutting the list silently.
                    _logger.LogWarning("The tag filter '{Tag}' matches {Count} surveys, {Stale} of them waiting for the reindex step; those are left out of the filter until the reindex step reaches them.", tag, tagged.Length, staleTagged.Count);
                    tagged = null;
                }
            }

            if (tagged is null)
            {
                root = root.With<SurveyTagIndex>(index => index.Tag == tag);
            }
        }

        var q = root.With<SurveyIndex>();

        if (tagged is not null)
        {
            // Only while some surveys wait for the reindex step, and only when they fit in one IN list (above).
            var ids = tagged;
            q = q.Where(index => index.ItemId.IsIn(ids));
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            q = q.Where(index => index.Title.Contains(search) || index.Name.Contains(search));
        }

        if (!string.IsNullOrEmpty(query.Kind))
        {
            var kind = query.Kind;
            q = q.Where(index => index.Kind == kind);
        }

        if (!string.IsNullOrEmpty(query.Status))
        {
            var status = query.Status;
            q = q.Where(index => index.Status == status);
        }
        else
        {
            var archived = nameof(SurveyStatus.Archived);
            q = q.Where(index => index.Status != archived);
        }

        if (!query.IncludeOwned)
        {
            q = q.Where(index => index.OwnerRef == null || index.OwnerRef == string.Empty);
        }

        if (!string.IsNullOrEmpty(query.OwnerId))
        {
            var ownerId = query.OwnerId;
            var staleOwned = stale
                .Where(survey => string.Equals(survey.OwnerId, ownerId, StringComparison.Ordinal))
                .Select(survey => survey.ItemId)
                .ToArray();

            if (!SurveyIdChunks.FitsOneList(staleOwned.Length))
            {
                _logger.LogWarning("{Count} surveys of the owner '{OwnerId}' wait for the reindex step; the first {Size} are listed until the reindex step reaches the rest.", staleOwned.Length, ownerId, SurveyIdChunks.Size);
                staleOwned = staleOwned[..SurveyIdChunks.Size];
            }

            q = staleOwned.Length == 0
                ? q.Where(index => index.OwnerId == ownerId)
                : q.Where(index => index.OwnerId == ownerId || index.ItemId.IsIn(staleOwned));
        }

        var count = await q.CountAsync(cancellationToken);
        var items = await q
            .OrderByDescending(index => index.ModifiedUtc)
            .ThenByDescending(index => index.Id)
            .Skip(Math.Max(0, skip))
            .Take(Math.Max(1, take))
            .ListAsync(cancellationToken);

        return (count, items.ToList());
    }

    /// <summary>
    /// Lists the tags in use, for the tag filter: of every survey, or of the surveys a user owns.
    /// </summary>
    /// <param name="ownerId">The id of the user whose surveys' tags are listed, or <see langword="null"/> for every survey.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The tags, sorted; at most <see cref="MaxTags"/>.</returns>
    public async Task<IReadOnlyList<string>> ListTagsAsync(string ownerId, CancellationToken cancellationToken = default)
    {
        var sql = new SurveyIndexSql(_session);
        var builder = sql.Builder<SurveyTagIndex>(SurveysConstants.CollectionName);
        var parameters = new DynamicParameters();
        var tag = sql.Column(nameof(SurveyTagIndex.Tag));

        builder.GroupBy(tag);
        builder.Selector(tag);

        if (!string.IsNullOrEmpty(ownerId))
        {
            builder.WhereAnd($"{sql.Column(nameof(SurveyTagIndex.OwnerId))} = @OwnerId");
            parameters.Add("OwnerId", ownerId);
        }

        builder.OrderBy(tag);
        builder.Take(SurveyIndexSql.Number(MaxTags));

        // The tags of surveys whose rows wait for the reindex step are read from the surveys.
        var stale = (await _reader.ListNotReindexedAsync(cancellationToken))
            .Where(survey => string.IsNullOrEmpty(ownerId) || string.Equals(survey.OwnerId, ownerId, StringComparison.Ordinal))
            .SelectMany(survey => survey.Tags ?? [])
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(SurveyTagIndexProvider.Normalize);

        return (await sql.QueryAsync(builder, parameters, cancellationToken))
            .Select(row => row.Values.FirstOrDefault() as string)
            .Concat(stale)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
