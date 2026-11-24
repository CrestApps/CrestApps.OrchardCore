using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
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
/// an anonymous survey follow the small-group rule of <see cref="SurveyGroupGate"/>, counted on the responses that
/// pass the viewer's filters, and their times show the day only.
/// </summary>
/// <typeparam name="TDocument">The documents read.</typeparam>
/// <typeparam name="TIndex">The index they are read through.</typeparam>
/// <typeparam name="TRow">The rows.</typeparam>
public abstract class SurveysDataSetBase<TDocument, TIndex, TRow> : IndexedReportDataSet<TDocument, TIndex, TRow>, ISurveysDataSet
    where TDocument : class
    where TIndex : MapIndex
    where TRow : class
{
    // The status (and test flag) of a row is a group of its own under the small-group rule, so a condition on it
    // narrows which groups the consumer keeps, not which responses count.
    private static readonly HashSet<string> _ungatedFields = new(StringComparer.OrdinalIgnoreCase) { "Status", "IsTest" };

    private readonly SurveyDataSetAccess _access;
    private readonly Dictionary<string, (Func<TRow, object> Read, DataFieldType Type)> _readers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SurveyAnonymousAttributes> _attributes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    private bool _prepared;
    private bool _readableAll;

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
        // LoadAsync reads the surveys first, to widen the date range of an anonymous survey before the index is read.
        var all = _readableAll;

        if (!_prepared)
        {
            await PrepareAsync(context, cancellationToken);
            all = _readableAll;
        }

        _prepared = false;

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
        // The rows of an anonymous survey follow the shared small-group rule (SurveyGroupGate), counted on the responses
        // that survive the viewer's filters: the engine filters rows again after this read, so a filter on an answer (or
        // on the status) could otherwise narrow a released survey to one person. Each condition the engine passes down is
        // evaluated here before a response counts; a condition this can't evaluate counts nothing, which only hides more.
        // A survey's rows show once its completed, non-test responses reach the minimum, and each status once that many
        // real responses share it; a group that never does is dropped. Test rows never count; they show once the
        // survey's completed responses reach the minimum. The collector, version and context kind of each row are groups
        // too (SurveyAnonymousAttributes).
        //
        // An anonymous survey's rows are also held until the read ends, then written in the order of their response ids,
        // so where a row sits among the rows read by time says nothing of when it came in; and only once the day they last
        // changed on has ended in the site's time zone, when their change time (the data set's date) reads the end of that
        // day. An incremental read, which keeps the rows whose change time is past the largest it read, then never skips
        // one: a row is released at the time it reads.
        var conditions = (query.Conditions ?? [])
            .Where(condition => condition is not null && !_ungatedFields.Contains(condition.Field ?? string.Empty))
            .ToArray();
        var held = new Dictionary<string, List<(TRow Row, SurveyResponse Response)>>(StringComparer.Ordinal);
        var now = _access.UtcNow();

        await PrepareAsync(query.Context, cancellationToken);
        _prepared = true;

        await foreach (var row in base.LoadAsync(WholeDaysQuery(query), cancellationToken))
        {
            if (!Surveys.ContainsKey(SurveyIdOf(row) ?? string.Empty))
            {
                continue;
            }

            var response = ResponseOf(row);
            var surveyId = response?.SurveyId ?? string.Empty;
            var minimum = response is null ? 0 : SurveyAnonymity.MinimumGroupSize(Surveys.GetValueOrDefault(surveyId), Settings);

            if (minimum == 0)
            {
                yield return row;

                continue;
            }

            if (SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone) > now)
            {
                continue;
            }

            if (!held.TryGetValue(surveyId, out var rows))
            {
                held[surveyId] = rows = [];
            }

            rows.Add((row, response));
        }

        foreach (var (surveyId, rows) in held)
        {
            foreach (var row in await ReleaseAsync(Surveys[surveyId], rows, conditions, cancellationToken))
            {
                yield return row;
            }
        }
    }

    // Reads the surveys the principal may read, the site settings and the time zone, once per read.
    private async Task PrepareAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        var (all, surveys) = await ReadableAsync(context, cancellationToken);

        Surveys = surveys;
        _readableAll = all;
        (Settings, TimeZone) = await _access.AnonymityAsync();
    }

    // Decides which held rows of one anonymous survey show, and in which order.
    private async Task<List<TRow>> ReleaseAsync(Survey survey, List<(TRow Row, SurveyResponse Response)> rows, DataCondition[] conditions, CancellationToken cancellationToken)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, Settings);
        var attributes = await AttributesAsync(survey, minimum, cancellationToken);

        foreach (var (_, response) in rows)
        {
            if (!_labels.ContainsKey(response.ItemId ?? string.Empty))
            {
                _labels[response.ItemId ?? string.Empty] = await _access.LabelAsync(survey, response, cancellationToken);
            }
        }

        var completed = new HashSet<string>(StringComparer.Ordinal);
        var counted = new Dictionary<SurveyResponseStatus, HashSet<string>>();

        foreach (var (row, response) in rows)
        {
            if (response.IsTest || attributes.Of(response).Hidden || !Matches(row, conditions))
            {
                continue;
            }

            if (!counted.TryGetValue(response.Status, out var ids))
            {
                counted[response.Status] = ids = new HashSet<string>(StringComparer.Ordinal);
            }

            ids.Add(response.ItemId ?? string.Empty);

            if (CountsTowardGroupSize(response))
            {
                completed.Add(response.ItemId ?? string.Empty);
            }
        }

        if (completed.Count < minimum)
        {
            return [];
        }

        // A status with no row that passes the filters stays hidden: the consumer would drop its rows anyway, and a row
        // this data set misjudged must not slip out in a group of one.
        bool Shows(SurveyResponse response)
        {
            if (response.IsTest)
            {
                return true;
            }

            var count = counted.GetValueOrDefault(response.Status)?.Count ?? 0;

            return count >= minimum && SurveyGroupGate.ShowsStatus(minimum, completed.Count, count);
        }

        return rows
            .Where(pair => !attributes.Of(pair.Response).Hidden && Shows(pair.Response))
            .OrderBy(pair => pair.Response.ItemId, StringComparer.Ordinal)
            .Select(pair => pair.Row)
            .ToList();
    }

    // The small-group rule of the per-row attributes of an anonymous survey, counted once per read.
    private async Task<SurveyAnonymousAttributes> AttributesAsync(Survey survey, int minimum, CancellationToken cancellationToken)
    {
        if (!_attributes.TryGetValue(survey.ItemId, out var attributes))
        {
            var queries = new SurveyResultsQueries(Session);

            _attributes[survey.ItemId] = attributes = SurveyAnonymousAttributes.Create(
                minimum,
                await queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.CollectorId), cancellationToken),
                await queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.VersionNumber), cancellationToken),
                await queries.CountCompletedByAsync(survey.ItemId, nameof(SurveyResponseIndex.ContextKind), cancellationToken));
        }

        return attributes;
    }

    // A copy of the query whose date range, on a read that may hold an anonymous survey's rows, is widened to whole days
    // in the site's time zone before it reaches the index: stepping the time of day of a filter can't tell, minute by
    // minute, when an anonymous response changed. The engine filters every row again on its own conditions.
    private DataSourceQuery WholeDaysQuery(DataSourceQuery query)
    {
        if (DateColumn is not { } dated || !Surveys.Values.Any(survey => SurveyAnonymity.MinimumGroupSize(survey, Settings) > 0))
        {
            return query;
        }

        var (from, to) = DataConditions.DateRange(query.Conditions, dated.Field);

        if (from is null && to is null)
        {
            return query;
        }

        var conditions = (query.Conditions ?? [])
            .Where(condition => condition is null || !string.Equals(condition.Field, dated.Field, StringComparison.Ordinal))
            .ToList();

        if (from is { } start && start > DateTime.MinValue.AddDays(2))
        {
            conditions.Add(new DataCondition { Field = dated.Field, Operator = DataFilterOperator.GreaterThanOrEqual, Values = [SurveyAnonymity.StartOfDayUtc(start, TimeZone)] });
        }

        if (to is { } end && end < DateTime.MaxValue.AddDays(-2))
        {
            conditions.Add(new DataCondition { Field = dated.Field, Operator = DataFilterOperator.LessThanOrEqual, Values = [SurveyAnonymity.EndOfDayUtc(end, TimeZone)] });
        }

        return new DataSourceQuery
        {
            DataSet = query.DataSet,
            Fields = query.Fields,
            Conditions = conditions,
            MaxRows = query.MaxRows,
            BatchSize = query.BatchSize,
            Parameters = query.Parameters,
            Context = query.Context,
        };
    }

    /// <summary>
    /// Gets whether a row passes the conditions a consumer passed down, as far as this data set can tell: a condition on
    /// a field it doesn't know, or with an operator it doesn't evaluate, fails, so the row doesn't count toward a
    /// group size (it is still read, and the consumer filters it).
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="conditions">The conditions.</param>
    /// <returns><see langword="true"/> when it passes every condition.</returns>
    internal bool Matches(TRow row, IReadOnlyCollection<DataCondition> conditions)
    {
        foreach (var condition in conditions)
        {
            if (!_readers.TryGetValue(condition.Field ?? string.Empty, out var reader)
                || !SurveyDataConditions.Matches(DataValues.Coerce(reader.Read(row), reader.Type), reader.Type, condition))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Declares a field of the data set and keeps how to read it, so the conditions on it can be evaluated before an
    /// anonymous survey's group sizes are counted.
    /// </summary>
    /// <param name="name">The stable technical name of the field.</param>
    /// <param name="displayName">The label shown to users.</param>
    /// <param name="type">The type of the field values.</param>
    /// <param name="read">Reads the value of the field from a row.</param>
    /// <param name="group">The optional group the field is listed under.</param>
    /// <returns>The field.</returns>
    protected new DataField AddField(string name, string displayName, DataFieldType type, Func<TRow, object> read, string group = null)
    {
        _readers[name] = (read, type);

        return base.AddField(name, displayName, type, read, group);
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
    /// Gets the label of a response as a row shows it: its number on an identified survey, or on an anonymous survey a
    /// keyed hash of its id (<see cref="SurveyAnonymity.ResponseLabel"/>), worked out before its rows are released.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The label.</returns>
    protected string LabelOf(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return IsAnonymous(response.SurveyId)
            ? _labels.GetValueOrDefault(response.ItemId ?? string.Empty, string.Empty)
            : SurveyAnonymity.ResponseLabel(Surveys.GetValueOrDefault(response.SurveyId ?? string.Empty), response, null);
    }

    /// <summary>
    /// Gets what a row of a response may show of its collector, version and context kind: everything on an identified
    /// survey; on an anonymous survey, only the values enough completed responses share (see
    /// <see cref="SurveyAnonymousAttributes"/>).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>What may show.</returns>
    protected SurveyRowAttributes AttributesOf(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return IsAnonymous(response.SurveyId) && _attributes.TryGetValue(response.SurveyId ?? string.Empty, out var attributes)
            ? attributes.Of(response)
            : IsAnonymous(response.SurveyId) ? new SurveyRowAttributes(false, false, false, false) : SurveyRowAttributes.All;
    }

    /// <summary>
    /// Gets the attempt number a row of a response shows: none on an anonymous survey, where it would tie a person's
    /// responses together.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The attempt number, or <see langword="null"/>.</returns>
    protected long? AttemptOf(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return IsAnonymous(response.SurveyId) ? null : response.AttemptNumber;
    }

    /// <summary>
    /// Gets the change time of a response as a row shows it: as stored, in UTC, or for an anonymous survey the end of the
    /// day it changed on in the site's time zone (the time its rows are released, so incremental reads skip none).
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <param name="utc">The time, in UTC.</param>
    /// <returns>The time to show, in UTC, or <see langword="null"/>.</returns>
    protected DateTime? ChangedOf(string surveyId, DateTime? utc)
    {
        if (utc is not { } value)
        {
            return null;
        }

        return IsAnonymous(surveyId)
            ? SurveyAnonymity.EndOfDayUtc(value, TimeZone)
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
    }

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
