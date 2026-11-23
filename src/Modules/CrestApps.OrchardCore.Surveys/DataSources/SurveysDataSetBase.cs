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
        // The rows of an anonymous survey follow the shared small-group rule (SurveyGroupGate), counted on the responses
        // that survive the viewer's filters: the engine filters rows again after this read, so a filter on an answer (or
        // on the status) could otherwise narrow a released survey to one person. Each condition the engine passes down is
        // evaluated here before a response counts; a condition this can't evaluate counts nothing, which only hides more.
        // A survey's rows wait until its completed, non-test responses reach the minimum, and each status waits until
        // that many real responses share it; a group that never does is dropped. Test rows never count; they show once
        // the survey's completed responses reach the minimum.
        var conditions = (query.Conditions ?? [])
            .Where(condition => condition is not null && !_ungatedFields.Contains(condition.Field ?? string.Empty))
            .ToArray();
        var groups = new Dictionary<string, AnonymousGroup>(StringComparer.Ordinal);

        await foreach (var row in base.LoadAsync(query, cancellationToken))
        {
            var response = ResponseOf(row);
            var surveyId = response?.SurveyId ?? string.Empty;
            var minimum = response is null ? 0 : SurveyAnonymity.MinimumGroupSize(Surveys.GetValueOrDefault(surveyId), Settings);

            if (minimum == 0)
            {
                yield return row;

                continue;
            }

            if (!groups.TryGetValue(surveyId, out var group))
            {
                groups[surveyId] = group = new AnonymousGroup(minimum);
            }

            if (!response.IsTest && Matches(row, conditions))
            {
                group.Count(response);
            }

            foreach (var released in group.Add(row, response))
            {
                yield return released;
            }
        }
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
    /// The rows of one anonymous survey held back until their groups are large enough.
    /// </summary>
    private sealed class AnonymousGroup
    {
        private readonly int _minimum;
        private readonly HashSet<string> _completed = new(StringComparer.Ordinal);
        private readonly Dictionary<SurveyResponseStatus, HashSet<string>> _counted = [];
        private readonly Dictionary<SurveyResponseStatus, List<TRow>> _held = [];
        private readonly HashSet<SurveyResponseStatus> _released = [];
        private readonly List<TRow> _heldTests = [];

        public AnonymousGroup(int minimum)
        {
            _minimum = minimum;
        }

        public void Count(SurveyResponse response)
        {
            if (!_counted.TryGetValue(response.Status, out var ids))
            {
                _counted[response.Status] = ids = new HashSet<string>(StringComparer.Ordinal);
            }

            ids.Add(response.ItemId ?? string.Empty);

            if (CountsTowardGroupSize(response))
            {
                _completed.Add(response.ItemId ?? string.Empty);
            }
        }

        public IEnumerable<TRow> Add(TRow row, SurveyResponse response)
        {
            var open = _completed.Count >= _minimum;

            if (response.IsTest)
            {
                if (open)
                {
                    return [row];
                }

                _heldTests.Add(row);

                return [];
            }

            if (_released.Contains(response.Status))
            {
                return [row];
            }

            if (!_held.TryGetValue(response.Status, out var rows))
            {
                _held[response.Status] = rows = [];
            }

            rows.Add(row);

            return open ? Release() : [];
        }

        private List<TRow> Release()
        {
            var released = new List<TRow>(_heldTests);
            _heldTests.Clear();

            foreach (var (status, rows) in _held.ToArray())
            {
                // A status with no row that passes the filters stays held: the consumer would drop its rows anyway, and
                // a row this data set misjudged must not slip out in a group of one.
                var count = _counted.GetValueOrDefault(status)?.Count ?? 0;

                if (count >= _minimum && SurveyGroupGate.ShowsStatus(_minimum, _completed.Count, count))
                {
                    _released.Add(status);
                    _held.Remove(status);
                    released.AddRange(rows);
                }
            }

            return released;
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
