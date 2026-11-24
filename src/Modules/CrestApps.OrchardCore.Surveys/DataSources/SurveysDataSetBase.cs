using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using CrestApps.OrchardCore.Reports.Designer.DataSources;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Results;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
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
    private readonly Dictionary<string, IReadOnlyDictionary<SurveyResponseGroup, long>> _groupCounts = new(StringComparer.Ordinal);
    private readonly Dictionary<string, Release> _releases = new(StringComparer.Ordinal);
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
        // their joint key (SurveyAnonymousAttributes). Only responses whose day has ended count toward a release.
        //
        // Each row has its own release moment (Release.MomentOf): the latest of the end of the day it last changed on, the
        // moment the minimum-th of the survey's counted completed responses ended theirs, the same moment within its
        // status, and the same within its joint key (or within "Other", the pool of the keys too small to show). A row
        // shows once its moment has passed, and its change time (the data set's date) reads that moment, so a row held
        // back until its group or status grew is released with a time past an incremental read's watermark, never under it.
        //
        // A condition on the survey itself (its id or name) that rules a survey out skips it: none of its responses is
        // counted or read. When no other condition is left, the counts and moments come from the index in SQL, without
        // reading a document; otherwise the survey's documents are read once to count them.
        //
        // An anonymous survey's rows are read apart from the others, after its count, a page at a time in the order of
        // their random response ids (so where a row sits says nothing of when it came in, and no row is held in memory),
        // and written one for one between the other rows (so a capped read or a preview still shows some of them).
        var conditions = (query.Conditions ?? [])
            .Where(condition => condition is not null && !_ungatedFields.Contains(condition.Field ?? string.Empty) && !IsDate(condition.Field))
            .ToArray();

        await PrepareAsync(query.Context, cancellationToken);

        var now = _access.UtcNow();
        var anonymous = new List<(Survey Survey, Release Release)>();

        // Counted afresh on every read: a scope can read the same data set more than once (a preview, then a run).
        _attributes.Clear();
        _groupCounts.Clear();
        _releases.Clear();

        foreach (var survey in Surveys.Values.Where(IsReadApart).OrderBy(survey => survey.ItemId, StringComparer.Ordinal))
        {
            if (!MatchesSurvey(survey, conditions, out var rowConditions))
            {
                continue;
            }

            if (await CountAsync(survey, rowConditions, now, cancellationToken) is { } release && release.From <= now)
            {
                _releases[survey.ItemId] = release;
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

    // Splits the conditions into those on the survey itself, evaluated here once for the whole survey, and those on its
    // rows; false when a condition on the survey rules it out.
    private bool MatchesSurvey(Survey survey, DataCondition[] conditions, out DataCondition[] rowConditions)
    {
        var rest = new List<DataCondition>();

        foreach (var condition in conditions)
        {
            if (_readers.TryGetValue(condition.Field ?? string.Empty, out var reader) && TryGetSurveyValue(condition.Field, survey, out var value))
            {
                if (!SurveyDataConditions.Matches(DataValues.Coerce(value, reader.Type), reader.Type, condition))
                {
                    rowConditions = [];

                    return false;
                }

                continue;
            }

            rest.Add(condition);
        }

        rowConditions = [.. rest];

        return true;
    }

    /// <summary>
    /// Gets the value a field holds on every row of a survey, for a field that holds the survey itself (its id), so a
    /// condition on it is evaluated once for the survey: a survey it rules out is neither counted nor read.
    /// </summary>
    /// <param name="field">The field.</param>
    /// <param name="survey">The survey.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the field holds the survey itself.</returns>
    private protected virtual bool TryGetSurveyValue(string field, Survey survey, out object value)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (string.Equals(field, "SurveyId", StringComparison.OrdinalIgnoreCase))
        {
            value = survey.ItemId;

            return true;
        }

        value = null;

        return false;
    }

    // Counts the responses of one anonymous survey that pass the conditions, over all of its responses (not only the rows
    // a date range or a watermark narrows the read to), and works out when its rows may be released; null when none may.
    private async Task<Release> CountAsync(Survey survey, DataCondition[] conditions, DateTime now, CancellationToken cancellationToken)
    {
        var minimum = SurveyAnonymity.MinimumGroupSize(survey, Settings);
        var (attributes, counts) = await AttributesAsync(survey, minimum, cancellationToken);

        if (conditions.Length == 0 && await CountInSqlAsync(survey, minimum, attributes, counts, now, cancellationToken) is { Done: true } counted)
        {
            return counted.Release;
        }

        // The minimum-th earliest end of day of the counted responses of each status, and of the completed, non-test
        // responses of each joint key large enough to show and of "Other" (keys are whole-survey groups, as the
        // attributes are, so no filter narrows them).
        var statuses = new Dictionary<SurveyResponseStatus, NthEarliest>();
        var groups = new Dictionary<SurveyResponseGroup, NthEarliest>();
        var other = new NthEarliest(minimum);

        await foreach (var (response, rows) in ReadApartAsync(survey, null, null, cancellationToken))
        {
            if (response.IsTest || attributes.Of(response).Hidden)
            {
                continue;
            }

            var endOfDay = SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone);

            if (CountsTowardGroupSize(response))
            {
                var group = SurveyResponseGroup.Of(response);

                if (counts.GetValueOrDefault(group) >= minimum)
                {
                    Queue(groups, group, minimum).Add(endOfDay);
                }
                else
                {
                    other.Add(endOfDay);
                }
            }

            if (rows.Any(row => Matches(row, conditions)))
            {
                Queue(statuses, response.Status, minimum).Add(endOfDay);
            }
        }

        return Release.Create(attributes, counts, minimum, TimeZone, Moments(statuses), Moments(groups), other.Nth, now);
    }

    // The counts and moments of one anonymous survey from the index, in SQL, without reading a document: when no condition
    // other than on the survey itself is left. Not done when more keys are involved than one statement can name.
    private async Task<(bool Done, Release Release)> CountInSqlAsync(Survey survey, int minimum, SurveyAnonymousAttributes attributes, IReadOnlyDictionary<SurveyResponseGroup, long> counts, DateTime now, CancellationToken cancellationToken)
    {
        var large = counts.Where(pair => pair.Value >= minimum).Select(pair => pair.Key).ToList();
        List<SurveyResponseGroup> hidden = attributes.OtherIsLarge ? [] : [.. counts.Where(pair => pair.Value < minimum).Select(pair => pair.Key)];

        if (large.Count > SurveyResponseGroupFilter.MaxGroups || hidden.Count > SurveyResponseGroupFilter.MaxGroups)
        {
            return (false, null);
        }

        var queries = new SurveyResultsQueries(Session);
        var statuses = new Dictionary<SurveyResponseStatus, DateTime>();

        foreach (var (status, count) in await queries.CountByStatusAsync(new SurveyResultsScope { SurveyId = survey.ItemId, HiddenGroups = hidden }, cancellationToken))
        {
            if (count >= minimum && await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = survey.ItemId, Statuses = [status], HiddenGroups = hidden }, minimum, null, cancellationToken) is { } changed)
            {
                statuses[status] = SurveyAnonymity.EndOfDayUtc(changed, TimeZone);
            }
        }

        // Nothing is released before the completed responses reach the minimum, so the keys need no query.
        if (!statuses.TryGetValue(SurveyResponseStatus.Completed, out var from) || from > now)
        {
            return (true, null);
        }

        var groups = new Dictionary<SurveyResponseGroup, DateTime>();

        foreach (var group in large)
        {
            if (await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = survey.ItemId, CompletedOnly = true }, minimum, group, cancellationToken) is { } changed)
            {
                groups[group] = SurveyAnonymity.EndOfDayUtc(changed, TimeZone);
            }
        }

        DateTime? other = null;

        if (attributes.OtherIsLarge && await queries.NthEarliestChangeAsync(new SurveyResultsScope { SurveyId = survey.ItemId, CompletedOnly = true, HiddenGroups = large }, minimum, null, cancellationToken) is { } pooled)
        {
            other = SurveyAnonymity.EndOfDayUtc(pooled, TimeZone);
        }

        return (true, Release.Create(attributes, counts, minimum, TimeZone, statuses, groups, other, now));
    }

    private static NthEarliest Queue<TKey>(Dictionary<TKey, NthEarliest> queues, TKey key, int minimum)
        where TKey : notnull
    {
        if (!queues.TryGetValue(key, out var queue))
        {
            queues[key] = queue = new NthEarliest(minimum);
        }

        return queue;
    }

    private static Dictionary<TKey, DateTime> Moments<TKey>(Dictionary<TKey, NthEarliest> queues)
        where TKey : notnull
        => queues.Where(pair => pair.Value.Nth is not null).ToDictionary(pair => pair.Key, pair => pair.Value.Nth.Value, queues.Comparer);

    // The rows of the anonymous surveys that may show, survey after survey, each in the order of its response ids.
    private async IAsyncEnumerable<TRow> ReleasedAsync(List<(Survey Survey, Release Release)> surveys, DataSourceQuery query, DateTime now, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        foreach (var (survey, release) in surveys)
        {
            // A condition on the change time is on the time shown, a row's release moment, never before the end of the day
            // it changed on. Past the latest moment anything of the survey was released at, the time shown is the end of
            // the row's own day, so the raw range read starts a day before the condition's. Before it, a row released
            // late shows a time later than the day it changed on, so the read starts at the beginning. The engine filters
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

                if (from is { } start && start > DateTime.MinValue.AddDays(2) && start > release.Latest)
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
                if (release.MomentOf(response) is not { } moment || moment > now)
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

    // The small-group rule of the per-row attributes of an anonymous survey, and the counts by joint key it was made
    // from, counted once per read.
    private async Task<(SurveyAnonymousAttributes Attributes, IReadOnlyDictionary<SurveyResponseGroup, long> Counts)> AttributesAsync(Survey survey, int minimum, CancellationToken cancellationToken)
    {
        if (!_attributes.TryGetValue(survey.ItemId, out var attributes) || !_groupCounts.TryGetValue(survey.ItemId, out var counts))
        {
            counts = await new SurveyResultsQueries(Session).CountByGroupAsync(survey.ItemId, completedOnly: true, cancellationToken);
            _groupCounts[survey.ItemId] = counts;
            _attributes[survey.ItemId] = attributes = SurveyAnonymousAttributes.Create(minimum, counts);
        }

        return (attributes, counts);
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
    /// Gets the change time of a response as a row shows it: as stored, in UTC, or for an anonymous survey the moment its
    /// row is released: the end of the day it changed on in the site's time zone, or the moment its survey, its status or
    /// its joint key reached the minimum group size if that is later. An incremental read, which keeps the rows whose
    /// change time is past the largest it read, then never skips a row released late.
    /// </summary>
    /// <param name="response">The response.</param>
    /// <returns>The time to show, in UTC.</returns>
    protected DateTime ChangedOf(SurveyResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (!IsAnonymous(response.SurveyId))
        {
            return DateTime.SpecifyKind(response.ModifiedUtc, DateTimeKind.Utc);
        }

        return _releases.TryGetValue(response.SurveyId ?? string.Empty, out var release) && release.MomentOf(response) is { } moment
            ? moment
            : SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, TimeZone);
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

    // The minimum-th earliest of the times added: a queue of at most that many, latest first.
    private sealed class NthEarliest
    {
        private readonly int _n;
        private readonly PriorityQueue<DateTime, DateTime> _earliest = new(Comparer<DateTime>.Create((left, right) => right.CompareTo(left)));

        public NthEarliest(int n)
        {
            _n = Math.Max(1, n);
        }

        public DateTime? Nth
            => _earliest.Count >= _n ? _earliest.Peek() : null;

        public void Add(DateTime value)
        {
            _earliest.Enqueue(value, value);

            if (_earliest.Count > _n)
            {
                _earliest.Dequeue();
            }
        }
    }

    // What one anonymous survey's count decided: which of its rows show, and from when each does.
    private sealed class Release
    {
        private readonly SurveyAnonymousAttributes _attributes;
        private readonly IReadOnlyDictionary<SurveyResponseGroup, long> _counts;
        private readonly int _minimum;
        private readonly TimeZoneInfo _timeZone;
        private readonly Dictionary<SurveyResponseStatus, DateTime> _statuses;
        private readonly Dictionary<SurveyResponseGroup, DateTime> _groups;
        private readonly DateTime? _other;

        private Release(
            SurveyAnonymousAttributes attributes,
            IReadOnlyDictionary<SurveyResponseGroup, long> counts,
            int minimum,
            TimeZoneInfo timeZone,
            Dictionary<SurveyResponseStatus, DateTime> statuses,
            Dictionary<SurveyResponseGroup, DateTime> groups,
            DateTime? other,
            DateTime from,
            DateTime now)
        {
            _attributes = attributes;
            _counts = counts;
            _minimum = minimum;
            _timeZone = timeZone;
            _statuses = statuses;
            _groups = groups;
            _other = other;
            From = from;
            Latest = statuses.Values
                .Concat(groups.Values)
                .Append(other ?? from)
                .Append(from)
                .Where(moment => moment <= now)
                .DefaultIfEmpty(from)
                .Max();
        }

        // The moment the survey's rows are released: once at least the minimum of its counted completed responses have
        // ended the day they last changed on.
        public DateTime From { get; }

        // The latest moment, up to now, at which the survey, a status or a joint key was released: a row whose own day
        // ended after it shows the end of that day.
        public DateTime Latest { get; }

        // Null when fewer than the minimum of the counted completed responses have ended their day.
        public static Release Create(
            SurveyAnonymousAttributes attributes,
            IReadOnlyDictionary<SurveyResponseGroup, long> counts,
            int minimum,
            TimeZoneInfo timeZone,
            Dictionary<SurveyResponseStatus, DateTime> statuses,
            Dictionary<SurveyResponseGroup, DateTime> groups,
            DateTime? other,
            DateTime now)
            => statuses.TryGetValue(SurveyResponseStatus.Completed, out var from)
                ? new Release(attributes, counts, minimum, timeZone, statuses, groups, other, from, now)
                : null;

        // The moment a response's row is released, or null when it never is: its joint key is too small and "Other" too,
        // or too few responses that pass the filters share its status. A test row shows once the survey's rows do.
        public DateTime? MomentOf(SurveyResponse response)
        {
            if (_attributes.Of(response).Hidden)
            {
                return null;
            }

            var moment = Later(SurveyAnonymity.EndOfDayUtc(response.ModifiedUtc, _timeZone), From);

            if (response.IsTest)
            {
                return moment;
            }

            if (!_statuses.TryGetValue(response.Status, out var status))
            {
                return null;
            }

            var group = SurveyResponseGroup.Of(response);
            var joint = _counts.GetValueOrDefault(group) >= _minimum
                ? _groups.TryGetValue(group, out var released) ? released : (DateTime?)null
                : _other;

            return joint is { } value ? Later(Later(moment, status), value) : null;
        }

        private static DateTime Later(DateTime left, DateTime right)
            => left > right ? left : right;
    }
}
