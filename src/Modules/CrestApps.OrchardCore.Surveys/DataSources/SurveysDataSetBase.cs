using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Services;
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
    /// Narrows the index rows read to those of the given surveys, and to the rows the data set reads at all (such as
    /// completed responses only).
    /// </summary>
    /// <param name="rows">The index rows.</param>
    /// <param name="surveyIds">The survey ids, at most <see cref="SurveyIdChunks.Size"/> of them; or <see langword="null"/>
    /// to read every survey's rows, when the principal may read every survey or more surveys than one <c>IN</c> list holds
    /// (each row is then checked against <see cref="Surveys"/>).</param>
    /// <returns>The narrowed rows.</returns>
    protected abstract IQueryIndex<TIndex> OfSurveys(IQueryIndex<TIndex> rows, string[] surveyIds);

    /// <summary>
    /// Gets the survey a row belongs to. By default, the survey of <see cref="ResponseOf"/>.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <returns>The survey id, or <see langword="null"/>.</returns>
    protected virtual string SurveyIdOf(TRow row)
        => ResponseOf(row)?.SurveyId;

    /// <summary>
    /// Lists the surveys the principal of a read may read, and whether that is every survey. By default, from
    /// <see cref="SurveyDataSetAccess.ReadableAsync"/>.
    /// </summary>
    /// <param name="context">The context of the read.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>Whether every survey is readable, and the readable surveys, by id.</returns>
    private protected virtual Task<(bool All, IReadOnlyDictionary<string, Survey> Surveys)> ReadableAsync(DataSourceContext context, CancellationToken cancellationToken)
        => _access.ReadableAsync(context, cancellationToken);

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
        var (all, surveys) = await ReadableAsync(context, cancellationToken);

        Surveys = surveys;
        (Settings, TimeZone) = await _access.AnonymityAsync();

        if (Surveys.Count == 0)
        {
            return null;
        }

        // SQL Server refuses a statement with more than 2,100 parameters, so the read names the surveys only when they fit
        // in one IN list. Otherwise (and when every survey is readable) it reads every survey's rows, and LoadAsync keeps
        // the rows of the readable surveys only.
        var ids = !all && SurveyIdChunks.FitsOneList(Surveys.Count) ? Surveys.Keys.ToArray() : null;

        return rows => OfSurveys(rows, ids);
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<TRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The rows of an anonymous survey wait until the completed, non-test responses read reach the survey's minimum
        // group size; a group that never does is dropped. In-progress, abandoned and test responses never count, so they
        // can't pad a group past the threshold.
        var held = new Dictionary<string, (HashSet<string> Responses, List<TRow> Rows)>(StringComparer.Ordinal);
        var released = new HashSet<string>(StringComparer.Ordinal);

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            if (!Surveys.ContainsKey(SurveyIdOf(row) ?? string.Empty))
            {
                continue;
            }

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

            if (CountsTowardGroupSize(response))
            {
                group.Responses.Add(response.ItemId ?? string.Empty);
            }

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
    /// Gets whether a response counts toward an anonymous survey's minimum group size: only completed, non-test
    /// responses do, as in the reports.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns><see langword="true"/> when it counts.</returns>
    internal static bool CountsTowardGroupSize(SurveyResponse response)
        => response is { IsTest: false, Status: SurveyResponseStatus.Completed };

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
