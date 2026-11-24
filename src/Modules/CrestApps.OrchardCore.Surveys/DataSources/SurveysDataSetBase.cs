using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using Microsoft.AspNetCore.Authorization;
using OrchardCore.DataSources;
using YesSql;
using YesSql.Indexes;
using YesSql.Services;
using ISession = YesSql.ISession;

namespace CrestApps.OrchardCore.Surveys.DataSources;

/// <summary>
/// The base of the Surveys data sets: a principal needs View own survey responses, and reads only the rows of the
/// surveys whose responses it may read. Who responded shows on identified surveys only (the one identity rule of
/// <see cref="Core.Services.SurveyRespondentNames"/>: reading a survey's responses lets you see who gave them). Rows of
/// an anonymous survey follow the small-group rule of <see cref="SurveyGroupGate"/>, counted on all of the survey's
/// responses that pass the viewer's filters other than dates, and their times show the day only, in the site's time
/// zone.
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

    // YesSql's NOT IN operator, found from a sample expression so it can be applied to the survey column of any index.
    private static readonly MethodInfo _isNotIn = ((MethodCallExpression)((Expression<Func<string, bool>>)(value => value.IsNotIn(Array.Empty<string>()))).Body).Method;

    // Stands in for "greater than" between two strings in an expression YesSql turns into SQL; never called.
    private static readonly MethodInfo _isAfter = typeof(SurveysDataSetBase<TDocument, TIndex, TRow>).GetMethod(nameof(IsAfter), BindingFlags.NonPublic | BindingFlags.Static);

    private readonly SurveyDataSetAccess _access;
    private readonly Dictionary<string, (Func<TRow, object> Read, DataFieldType Type)> _readers = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SurveyAnonymousAttributes> _attributes = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DateTime> _releasedFrom = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _labels = new(StringComparer.Ordinal);
    private string[] _anonymousSurveyIds = [];
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
    /// Gets the time zone the days of anonymous responses are read in: the site's, never the reader's
    /// (<see cref="SurveyAnonymousDays"/>). Set when a read starts.
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
    /// Gets the index rows of one anonymous survey the data set reads (as <see cref="OfSurveys"/> narrows them), for the
    /// read of its rows apart from the others, or <see langword="null"/> when the data set's rows aren't responses. A data
    /// set that returns a condition must read <see cref="Core.Indexes.SurveyResponseIndex"/>, whose rows are one per
    /// response and carry its id.
    /// </summary>
    /// <param name="surveyId">The survey.</param>
    /// <returns>The condition, or <see langword="null"/>.</returns>
    protected virtual Expression<Func<TIndex, bool>> AnonymousRowsOf(string surveyId)
        => null;

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

    /// <summary>
    /// Completes a row a document gave (<see cref="IndexedReportDataSet{TDocument, TIndex, TRow}.GetRows"/>) into the
    /// rows the data set shows: fills in what needs a lookup (names, the label, what an anonymous row shows of its
    /// attributes) or expands it. It runs before the conditions are evaluated for an anonymous survey's group sizes, so a
    /// condition on a field filled in here counts the same rows the consumer keeps. By default, the row as it is.
    /// </summary>
    /// <param name="row">The row.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    protected virtual async IAsyncEnumerable<TRow> CompleteAsync(TRow row, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;

        yield return row;
    }

    /// <inheritdoc />
    protected override async Task<Func<IQueryIndex<TIndex>, IQueryIndex<TIndex>>> RestrictAsync(DataSourceContext context, CancellationToken cancellationToken)
    {
        // LoadAsync reads the surveys first, to tell which are anonymous before the index is read.
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
        // the rows of the readable surveys only. Anonymous surveys are read apart (LoadAsync), so they are left out here
        // when their ids fit in one NOT IN list (otherwise LoadAsync skips their rows).
        var ids = !all && SurveyIdChunks.FitsOneList(Surveys.Count) ? Surveys.Keys.Where(id => !_anonymousSurveyIds.Contains(id, StringComparer.Ordinal)).ToArray() : null;

        if (ids is { Length: 0 })
        {
            return null;
        }

        var excluded = ids is null && _anonymousSurveyIds.Length > 0 && SurveyIdChunks.FitsOneList(_anonymousSurveyIds.Length)
            ? NotInSurveys(_anonymousSurveyIds)
            : null;

        return rows => excluded is null ? OfSurveys(rows, ids) : OfSurveys(rows, ids).Where(excluded);
    }

    /// <inheritdoc />
    protected override async IAsyncEnumerable<TRow> LoadAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // The rows of an anonymous survey follow the shared small-group rule (SurveyGroupGate), counted on all of the
        // survey's responses that pass the viewer's filters: the engine filters rows again after this read, so a filter on
        // an answer (or on the status) could otherwise narrow a released survey to one person. Each condition the engine
        // passes down is evaluated here before a response counts; a condition this can't evaluate counts nothing, which
        // only hides more. Conditions on dates are left out of the count: the dates of an anonymous survey's rows show the
        // day only, and a pipeline's watermark (rows changed after the last run) must not narrow the count to the few rows
        // that changed since, or a low-traffic survey would never release a row to an incremental read. A survey's rows
        // show once its completed, non-test responses reach the minimum, and each status once that many real responses
        // share it; a group that never does is dropped. Test rows never count; they show once the survey's completed
        // responses reach the minimum. The collector, version and context kind of each row are a group too, decided on
        // their joint key (SurveyAnonymousAttributes).
        //
        // An anonymous survey's rows are read apart from the others, after its count, a page at a time in the order of
        // their random response ids (so where a row sits says nothing of when it came in, and no row is held in memory),
        // and written one for one between the other rows (so a capped read or a preview still shows some of them). A row
        // is released only once the day it last changed on has ended in the site's time zone, and once enough of the
        // survey's counted responses have ended theirs; its change time (the data set's date) reads that moment. An
        // incremental read, which keeps the rows whose change time is past the largest it read, then never skips one.
        var conditions = (query.Conditions ?? [])
            .Where(condition => condition is not null && !_ungatedFields.Contains(condition.Field ?? string.Empty) && !IsDate(condition.Field))
            .ToArray();

        await PrepareAsync(query.Context, cancellationToken);

        var now = _access.UtcNow();
        var anonymous = new List<(Survey Survey, Release Release)>();

        foreach (var survey in Surveys.Values.Where(IsReadApart).OrderBy(survey => survey.ItemId, StringComparer.Ordinal))
        {
            if (await CountAsync(survey, conditions, cancellationToken) is { } release && release.From <= now)
            {
                anonymous.Add((survey, release));
            }
        }

        _anonymousSurveyIds = [.. Surveys.Values.Where(IsReadApart).Select(survey => survey.ItemId)];
        _prepared = true;

        await using var others = OthersAsync(query, cancellationToken).GetAsyncEnumerator(cancellationToken);
        await using var held = ReleasedAsync(anonymous, query, now, cancellationToken).GetAsyncEnumerator(cancellationToken);
        var moreOthers = true;
        var moreHeld = true;

        while (moreOthers || moreHeld)
        {
            if (moreOthers && (moreOthers = await others.MoveNextAsync()))
            {
                yield return others.Current;
            }

            if (moreHeld && (moreHeld = await held.MoveNextAsync()))
            {
                yield return held.Current;
            }
        }
    }

    // The rows of every survey not read apart: identified surveys, and every row of a data set whose rows aren't responses.
    private async IAsyncEnumerable<TRow> OthersAsync(DataSourceQuery query, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await foreach (var seed in base.LoadAsync(WholeDaysQuery(query), cancellationToken))
        {
            var surveyId = SurveyIdOf(seed) ?? string.Empty;

            if (!Surveys.TryGetValue(surveyId, out var survey) || IsReadApart(survey))
            {
                continue;
            }

            await foreach (var row in CompleteAsync(seed, cancellationToken))
            {
                yield return row;
            }
        }
    }

    // Counts the responses of one anonymous survey that pass the conditions, over all of its responses (not only the rows
    // a date range or a watermark narrows the read to), and works out when its rows may be released; null when none may.
    private async Task<Release> CountAsync(Survey survey, DataCondition[] conditions, CancellationToken cancellationToken)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, Settings);
        var attributes = await AttributesAsync(survey, minimum, cancellationToken);
        var counted = new Dictionary<SurveyResponseStatus, int>();
        var completed = 0;

        // The minimum-th earliest end of day among the counted completed responses: before it passes, fewer than the
        // minimum have ended their day, so nothing is released. A queue of at most the minimum ends, latest first.
        var earliest = new PriorityQueue<DateTime, DateTime>(Comparer<DateTime>.Create((left, right) => right.CompareTo(left)));

        await foreach (var (response, rows) in ReadApartAsync(survey, null, null, cancellationToken))
        {
            if (response.IsTest || attributes.Of(response).Hidden || !rows.Any(row => Matches(row, conditions)))
            {
                continue;
            }

            counted[response.Status] = counted.GetValueOrDefault(response.Status) + 1;

            if (CountsTowardGroupSize(response))
            {
                completed++;
                earliest.Enqueue(SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone), SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone));

                if (earliest.Count > minimum)
                {
                    earliest.Dequeue();
                }
            }
        }

        if (completed < minimum || earliest.Count == 0)
        {
            return null;
        }

        var from = earliest.Peek();

        _releasedFrom[survey.ItemId] = from;

        return new Release(minimum, attributes, counted, completed, from);
    }

    // The rows of the anonymous surveys that may show, survey after survey, each in the order of its response ids.
    private async IAsyncEnumerable<TRow> ReleasedAsync(List<(Survey Survey, Release Release)> surveys, DataSourceQuery query, DateTime now, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var (survey, release) in surveys)
        {
            // A condition on the change time is on the time shown, the end of the day a row changed on (or the moment the
            // survey's rows were released, if later): the raw range read starts a day earlier, and the engine filters
            // every row again on the time shown.
            DateTime? fromUtc = null;
            DateTime? toUtc = null;

            if (DateColumn is { } dated)
            {
                var (from, to) = DataConditions.DateRange(query.Conditions, dated.Field);

                if (to is { } end && end < release.From)
                {
                    continue;
                }

                if (from is { } start && start > DateTime.MinValue.AddDays(2) && start > release.From)
                {
                    fromUtc = SurveyAnonymity.StartOfDayUtc(start, TimeZone).AddDays(-1);
                }

                if (to is { } last && last < DateTime.MaxValue.AddDays(-2))
                {
                    toUtc = last;
                }
            }

            await foreach (var (response, rows) in ReadApartAsync(survey, fromUtc, toUtc, cancellationToken))
            {
                if (SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone) > now || !release.Shows(response))
                {
                    continue;
                }

                foreach (var row in rows)
                {
                    yield return row;
                }
            }
        }
    }

    // Reads the responses of one anonymous survey a page at a time in the order of their ids (keyset paging, so a page
    // costs the same however many came before it), optionally within a raw range of change times, with their rows.
    private async IAsyncEnumerable<(SurveyResponse Response, List<TRow> Rows)> ReadApartAsync(Survey survey, DateTime? fromUtc, DateTime? toUtc, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (AnonymousRowsOf(survey.ItemId) is not { } rowsOf || Key() is not { } key)
        {
            yield break;
        }

        string last = null;

        while (true)
        {
            var documents = Session.Query<TDocument, TIndex>(rowsOf, Collection);

            if (DateColumn is { } dated)
            {
                if (fromUtc is { } from)
                {
                    documents = documents.Where(Compare(dated.Column, ExpressionType.GreaterThanOrEqual, from));
                }

                if (toUtc is { } to)
                {
                    documents = documents.Where(Compare(dated.Column, ExpressionType.LessThanOrEqual, to));
                }
            }

            if (last is not null)
            {
                documents = documents.Where(After(key, last));
            }

            var page = (await documents.OrderBy(Boxed(key)).Take(ReportIndexPages.PageSize).ListAsync(cancellationToken)).ToList();

            if (page.Count == 0)
            {
                yield break;
            }

            foreach (var document in page)
            {
                var rows = new List<TRow>();
                SurveyResponse response = null;

                foreach (var seed in GetRows(document))
                {
                    response ??= ResponseOf(seed);

                    if (response is not null && !_labels.ContainsKey(response.ItemId ?? string.Empty))
                    {
                        _labels[response.ItemId ?? string.Empty] = await _access.LabelAsync(survey, response, cancellationToken);
                    }

                    await foreach (var row in CompleteAsync(seed, cancellationToken))
                    {
                        rows.Add(row);
                    }
                }

                if (response is not null)
                {
                    yield return (response, rows);
                }
            }

            Session.Detach(page, Collection);

            if (page.Count < ReportIndexPages.PageSize)
            {
                yield break;
            }

            if ((last = KeyOf(page[^1])) is null)
            {
                yield break;
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
        _anonymousSurveyIds = [.. Surveys.Values.Where(IsReadApart).Select(survey => survey.ItemId)];
    }

    // Whether a survey's rows are read apart: an anonymous survey, in a data set whose rows are responses.
    private bool IsReadApart(Survey survey)
        => SurveyAnonymity.MinimumGroupSize(survey, Settings) > 0 && AnonymousRowsOf(survey.ItemId) is not null;

    // Whether a field holds a date or a time.
    private bool IsDate(string field)
        => _readers.TryGetValue(field ?? string.Empty, out var reader) && reader.Type is DataFieldType.DateTime or DataFieldType.Date;

    // The small-group rule of the per-row attributes of an anonymous survey, counted once per read.
    private async Task<SurveyAnonymousAttributes> AttributesAsync(Survey survey, int minimum, CancellationToken cancellationToken)
    {
        if (!_attributes.TryGetValue(survey.ItemId, out var attributes))
        {
            _attributes[survey.ItemId] = attributes = SurveyAnonymousAttributes.Create(
                minimum,
                await new SurveyResultsQueries(Session).CountByGroupAsync(survey.ItemId, completedOnly: true, cancellationToken));
        }

        return attributes;
    }

    // A copy of the query whose date range, on a read that may hold an anonymous survey's rows, is widened to whole days
    // in the site's time zone before it reaches the index: stepping the time of day of a filter can't tell, minute by
    // minute, when an anonymous response changed. The engine filters every row again on its own conditions. Data sets
    // whose rows are responses read anonymous surveys apart, so they never need it.
    private DataSourceQuery WholeDaysQuery(DataSourceQuery query)
    {
        if (DateColumn is not { } dated || !Surveys.Values.Any(survey => SurveyAnonymity.MinimumGroupSize(survey, Settings) > 0 && !IsReadApart(survey)))
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
    /// keyed hash of its id (<see cref="SurveyAnonymity.ResponseLabel"/>), worked out when its rows are read.
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
    /// survey; on an anonymous survey, only the values enough completed responses share on their joint key (see
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
    /// Gets how long a response took, in seconds, as a row shows it: in whole minutes on an anonymous survey
    /// (<see cref="SurveyAnonymity.ShownDuration"/>).
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The seconds, or <see langword="null"/> when it isn't completed.</returns>
    protected long? DurationOf(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.CompletedUtc is { } completed && completed >= response.StartedUtc
            ? (long)SurveyAnonymity.ShownDuration(completed - response.StartedUtc, IsAnonymous(response.SurveyId)).TotalSeconds
            : null;
    }

    /// <summary>
    /// Gets the change time of a response as a row shows it: as stored, in UTC, or for an anonymous survey the end of the
    /// day it changed on in the site's time zone, or the moment the survey's rows were released if that is later (the
    /// time its row is released, so incremental reads skip none).
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

        if (!IsAnonymous(surveyId))
        {
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        var endOfDay = SurveyAnonymity.EndOfDayUtc(value, TimeZone);

        return _releasedFrom.TryGetValue(surveyId ?? string.Empty, out var released) && released > endOfDay ? released : endOfDay;
    }

    /// <summary>
    /// Gets a time of a response as a row shows it: as stored, in UTC, or for an anonymous survey the start of its day in
    /// the site's time zone.
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

    // Never called: an expression YesSql turns into SQL compares the two strings with ">".
    private static bool IsAfter(string left, string right)
        => string.CompareOrdinal(left, right) > 0;

    // The response id column of the index, which anonymous surveys are read in the order of.
    private static Expression<Func<TIndex, string>> Key()
    {
        if (typeof(TIndex).GetProperty("ItemId") is not { PropertyType: var type } property || type != typeof(string))
        {
            return null;
        }

        var index = Expression.Parameter(typeof(TIndex), "index");

        return Expression.Lambda<Func<TIndex, string>>(Expression.Property(index, property), index);
    }

    private static string KeyOf(TDocument document)
        => document is SurveyResponse response ? response.ItemId : null;

    private static Expression<Func<TIndex, object>> Boxed(Expression<Func<TIndex, string>> key)
        => Expression.Lambda<Func<TIndex, object>>(Expression.Convert(key.Body, typeof(object)), key.Parameters);

    private static Expression<Func<TIndex, bool>> After(Expression<Func<TIndex, string>> key, string last)
        => Expression.Lambda<Func<TIndex, bool>>(
            Expression.GreaterThan(key.Body, Expression.Constant(last, typeof(string)), liftToNull: false, _isAfter),
            key.Parameters);

    private static Expression<Func<TIndex, bool>> Compare(Expression<Func<TIndex, DateTime>> column, ExpressionType comparison, DateTime value)
        => Expression.Lambda<Func<TIndex, bool>>(
            Expression.MakeBinary(comparison, column.Body, Expression.Constant(DateTime.SpecifyKind(value, DateTimeKind.Utc), typeof(DateTime))),
            column.Parameters);

    // The index rows of surveys other than some, on an index with a survey column.
    private static Expression<Func<TIndex, bool>> NotInSurveys(string[] surveyIds)
    {
        if (typeof(TIndex).GetProperty("SurveyId") is not { PropertyType: var type } property || type != typeof(string))
        {
            return null;
        }

        var index = Expression.Parameter(typeof(TIndex), "index");

        return Expression.Lambda<Func<TIndex, bool>>(
            Expression.Call(_isNotIn, Expression.Property(index, property), Expression.Constant(surveyIds, _isNotIn.GetParameters()[1].ParameterType)),
            index);
    }

    // What one anonymous survey's count decided: which of its rows show, and from when.
    private sealed class Release
    {
        private readonly int _minimum;
        private readonly SurveyAnonymousAttributes _attributes;
        private readonly Dictionary<SurveyResponseStatus, int> _counted;
        private readonly int _completed;

        public Release(int minimum, SurveyAnonymousAttributes attributes, Dictionary<SurveyResponseStatus, int> counted, int completed, DateTime from)
        {
            _minimum = minimum;
            _attributes = attributes;
            _counted = counted;
            _completed = completed;
            From = from;
        }

        // The moment the survey's rows are released: once at least the minimum of its counted completed responses have
        // ended the day they last changed on.
        public DateTime From { get; }

        // A status with no response that passes the filters stays hidden: the consumer would drop its rows anyway, and a
        // row this data set misjudged must not slip out in a group of one.
        public bool Shows(SurveyResponse response)
        {
            if (_attributes.Of(response).Hidden)
            {
                return false;
            }

            if (response.IsTest)
            {
                return true;
            }

            var count = _counted.GetValueOrDefault(response.Status);

            return count >= _minimum && SurveyGroupGate.ShowsStatus(_minimum, _completed, count);
        }
    }
}
