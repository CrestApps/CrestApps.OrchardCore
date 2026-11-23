using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Models;
using Dapper;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Services;

/// <summary>
/// Reads pages of the survey list with its filters.
/// </summary>
public sealed class SurveyListService
{
    private readonly ISession _session;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyListService"/> class.
    /// </summary>
    /// <param name="session">The YesSql session.</param>
    public SurveyListService(ISession session)
    {
        _session = session;
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

        if (!string.IsNullOrWhiteSpace(query.Tag))
        {
            var tag = SurveyTagIndexProvider.Normalize(query.Tag);
            root = root.With<SurveyTagIndex>(index => index.Tag == tag);
        }

        var q = root.With<SurveyIndex>();

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
            q = q.Where(index => index.OwnerId == ownerId);
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

        return (await sql.QueryAsync(builder, parameters, cancellationToken))
            .Select(row => row.Values.FirstOrDefault() as string)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
