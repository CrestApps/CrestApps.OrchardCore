using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// The base of the Surveys data sets: a principal needs View own survey responses, and reads only the rows of the
/// surveys whose responses it may read. Who responded shows on identified surveys only (the one identity rule of
/// <see cref="Core.Services.SurveyRespondentNames"/>: reading a survey's responses lets you see who gave them). Rows of
/// an anonymous survey are held back until the responses read reach its minimum group size, and their times show the
/// day only.
/// </summary>
/// <typeparam name="TDocument">The documents read.</typeparam>
/// <typeparam name="TIndex">The index they are read through.</typeparam>
/// <typeparam name="TRow">The rows.</typeparam>
public abstract class SurveysDataSetBase<TDocument, TIndex, TRow> : IndexedReportDataSet<TDocument, TIndex, TRow>, ISurveysDataSet
    where TDocument : class
    where TIndex : MapIndex
    where TRow : class
{
    private readonly SurveyDataSetAccess _access;

    private protected SurveysDataSetBase(
        DataSetDescriptor descriptor,
        ISession session,
        IAuthorizationService authorizationService,
        SurveyDataSetAccess access,
        string collection)
        : base(descriptor, session, authorizationService, collection, SurveysPermissions.ViewOwnSurveyResponses)
    {
        _access = access;
    }

    /// <summary>
    /// Gets the surveys the principal of the current read may read, by id. Set when a read starts.
    /// </summary>
    protected IReadOnlyDictionary<string, Survey> Surveys { get; private set; } = new Dictionary<string, Survey>();

    /// <summary>
    /// Gets the site settings of Surveys, for the minimum group size. Set when a read starts.
    /// </summary>
    protected SurveySettings Settings { get; private set; }

    /// <summary>
    /// Gets the time zone the days of anonymous responses are read in. Set when a read starts.
    /// </summary>
    protected TimeZoneInfo TimeZone { get; private set; } = TimeZoneInfo.Utc;

    /// <inheritdoc />
    protected override bool CanAggregate => false;

    /// <summary>
    /// Narrows the index rows read to those of the given surveys.
    /// </summary>
    /// <param name="rows">The index rows.</param>
    /// <param name="surveyIds">The survey ids.</param>
    /// <returns>The narrowed rows.</returns>
    protected abstract IQueryIndex<TIndex> OfSurveys(IQueryIndex<TIndex> rows, string[] surveyIds);

    /// <summary>
    /// Gets the response a row belongs to, so the minimum group size of anonymous surveys can count responses, or
    /// <see langword="null"/> when rows aren't responses.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The response, or <see langword="null"/>.</returns>
    protected virtual SurveyResponse ResponseOf(TRow row)
        => null;

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        Surveys = await _access.ReadableAsync(context, cancellationToken);
        (Settings, TimeZone) = await _access.AnonymityAsync();

        if (Surveys.Count == 0)
        {
            return null;
        }

        var ids = Surveys.Keys.ToArray();

        return rows => OfSurveys(rows, ids);
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<TRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The rows of an anonymous survey wait until the responses read reach the survey's minimum group size; a group
        // that never does is dropped.
        var held = new Dictionary<string, (HashSet<string> Responses, List<TRow> Rows)>(StringComparer.Ordinal);
        var released = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            var response = ResponseOf(row);
            var surveyId = response?.SurveyId ?? string.Empty;
            var minimum = response is null ? 0 : SurveyAnonymity.MinimumGroupSize(Surveys.GetValueOrDefault(surveyId), Settings);

            if (minimum == 0 || released.Contains(surveyId))
            {
                yield return row;

                continue;
            }

            if (!held.TryGetValue(surveyId, out var group))
            {
                held[surveyId] = group = (new HashSet<string>(StringComparer.Ordinal), []);
            }

            group.Responses.Add(response.ItemId ?? string.Empty);
            group.Rows.Add(row);

            if (group.Responses.Count >= minimum)
            {
                released.Add(surveyId);
                held.Remove(surveyId);

                foreach (var waiting in group.Rows)
                {
                    yield return waiting;
                }
            }
        }
    }

    /// <summary>
    /// Gets whether the rows of a survey may show who responded: on an identified survey the principal may read.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns><see langword="true"/> when they may.</returns>
    protected bool ShowsIdentity(string surveyId)
        => !SurveyDataSetAccess.IsAnonymous(Surveys.GetValueOrDefault(surveyId ?? string.Empty));

    /// <summary>
    /// Gets whether a survey is anonymous (or unknown).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    protected bool IsAnonymous(string surveyId)
        => SurveyDataSetAccess.IsAnonymous(Surveys.GetValueOrDefault(surveyId ?? string.Empty));

    /// <summary>
    /// Gets a time of a response as a row shows it: as stored, in UTC, or for an anonymous survey the start of its day.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The time to show, in UTC, or <see langword="null"/>.</returns>
    protected DateTime? TimeOf(string surveyId, DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        return IsAnonymous(surveyId)
            ? SurveyAnonymity.StartOfDayUtc(value, TimeZone)
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }
}
