using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using OrchardCore.Settings;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Keeps participation records: how many responses each respondent started and completed each day, without a response
/// id, so limits never link an anonymous respondent to their answers. For an anonymous survey a record also keeps no
/// time finer than the day and only a hash of the context, so it can't be joined to a response by its timestamps; the
/// cooldown of an anonymous survey is then counted from the end of the day of the last completion.
/// Days and weeks are those of the site's time zone, never the respondent's, so changing one's own time zone never
/// starts a new day. Every start, completion and reopen adds a record of its own and counts are sums, so two requests
/// at once never lose a count.
/// </summary>
public sealed class SurveyParticipationService
{
    /// <summary>
    /// How long participation records are kept: longer than the largest limit window.
    /// </summary>
    public const int RetentionDays = 45;

    private readonly SurveyParticipationStore _store;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ISurveyWeekStartProvider _weekStart;
    private readonly SurveyContextKindOptions _contextKinds;

    private string _timeZoneId;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyParticipationService"/> class.
    /// </summary>
    /// <param name="store">The participation store.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="clock">The clock, for the server's time zone when the site has none.</param>
    /// <param name="weekStart">The first day of the week.</param>
    /// <param name="contextKinds">The registered context kinds.</param>
    public SurveyParticipationService(
        SurveyParticipationStore store,
        ISiteService siteService,
        IClock clock,
        ISurveyWeekStartProvider weekStart,
        IOptions<SurveyContextKindOptions> contextKinds)
    {
        _store = store;
        _siteService = siteService;
        _clock = clock;
        _weekStart = weekStart;
        _contextKinds = contextKinds.Value;
    }

    /// <summary>
    /// Gets the key of a context for participation records.
    /// </summary>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <returns>The key, or an empty string.</returns>
    public static string ContextKey(RecordRef? context)
        => context?.ToString() ?? string.Empty;

    /// <summary>
    /// Gets the key of a context for the participation records of a survey: for an anonymous survey, the hash of the
    /// context its responses keep (the kind only for a context that can point to one person).
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <returns>The key, or an empty string.</returns>
    public string ContextKey(Survey survey, RecordRef? context)
    {
        if (!SurveyAnonymity.IsAnonymous(survey))
        {
            return ContextKey(context);
        }

        var key = ContextKey(SurveyAnonymity.StoredContext(survey, context, _contextKinds));

        return key.Length > 0 ? "h:" + SurveyRespondentKeys.Hash(key) : key;
    }

    /// <summary>
    /// Counts a respondent's responses in each limit window.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="respondentKey">The respondent key.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>The counts.</returns>
    public async Task<SurveyParticipationCounts> CountAsync(Survey survey, string respondentKey, RecordRef? context, DateTime utcNow)
    {
        var today = await TodayAsync(utcNow);
        var firstDay = await _weekStart.GetFirstDayOfWeekAsync();
        var weekStart = today.AddDays(-(((int)today.DayOfWeek - (int)firstDay + 7) % 7));
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var from = today.AddDays(-RetentionDays);
        var contextKey = ContextKey(survey, context);
        var rows = (await _store.ListAsync(survey.ItemId, respondentKey, from))
            .Where(row => string.Equals(row.ContextKey ?? string.Empty, contextKey, StringComparison.Ordinal))
            .ToList();

        var counts = new SurveyParticipationCounts();

        foreach (var row in rows)
        {
            Add(counts.Started, row.Day, row.StartedCount, today, weekStart, monthStart);
            Add(counts.Completed, row.Day, row.CompletedCount, today, weekStart, monthStart);

            var completed = row.LastCompletedUtc;

            if (completed is null && row.CompletedCount > 0)
            {
                // An anonymous survey keeps the day only: count from the end of that day, so a cooldown is never short.
                completed = await EndOfDayUtcAsync(row.Day);
            }

            if (completed is { } last && (counts.LastCompletedUtc is null || last > counts.LastCompletedUtc))
            {
                counts.LastCompletedUtc = last;
            }
        }

        return counts;
    }

    /// <summary>
    /// Records that a respondent started a response.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="respondentKey">The respondent key.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>A task.</returns>
    public Task RecordStartedAsync(Survey survey, string respondentKey, RecordRef? context, DateTime utcNow)
        => RecordAsync(survey, respondentKey, context, utcNow, row => row.StartedCount = 1);

    /// <summary>
    /// Records that a respondent completed a response.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="respondentKey">The respondent key.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <returns>A task.</returns>
    public Task RecordCompletedAsync(Survey survey, string respondentKey, RecordRef? context, DateTime utcNow)
        => RecordAsync(survey, respondentKey, context, utcNow, row =>
        {
            row.CompletedCount = 1;
            row.LastCompletedUtc = SurveyAnonymity.IsAnonymous(survey) ? null : utcNow;
        });

    /// <summary>
    /// Takes back the completion of a response that was reopened, on the day it was completed, so completing it again
    /// counts once. Nothing is taken back when that day counts no completion for the respondent.
    /// </summary>
    /// <param name="survey">The survey.</param>
    /// <param name="respondentKey">The respondent key.</param>
    /// <param name="context">The context, or <see langword="null"/>.</param>
    /// <param name="completedUtc">When the response was completed, in UTC.</param>
    /// <returns>A task.</returns>
    public async Task RecordReopenedAsync(Survey survey, string respondentKey, RecordRef? context, DateTime completedUtc)
    {
        ArgumentNullException.ThrowIfNull(survey);

        if (string.IsNullOrEmpty(respondentKey))
        {
            return;
        }

        var day = await TodayAsync(completedUtc);
        var contextKey = ContextKey(survey, context);
        var completed = (await _store.ListAsync(survey.ItemId, respondentKey, day))
            .Where(row => row.Day == day && string.Equals(row.ContextKey ?? string.Empty, contextKey, StringComparison.Ordinal))
            .Sum(row => row.CompletedCount);

        if (completed <= 0)
        {
            return;
        }

        await _store.CreateAsync(new SurveyParticipation
        {
            SurveyId = survey.ItemId,
            RespondentKey = respondentKey,
            ContextKey = contextKey,
            Day = day,
            CompletedCount = -1,
        });
    }

    /// <summary>
    /// Lists the index rows of participation records older than the retention, a batch at a time in the order of their
    /// index ids, so a cleanup can read past a record it could not delete.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="afterId">The index id of the last row of the previous batch, or 0 for the first batch.</param>
    /// <param name="batchSize">The most rows to list.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The rows.</returns>
    public async Task<IReadOnlyList<SurveyParticipationIndex>> ListExpiredAsync(DateTime utcNow, long afterId, int batchSize, CancellationToken cancellationToken = default)
    {
        var before = (await TodayAsync(utcNow)).AddDays(-RetentionDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var rows = await _store.YesSqlSession.QueryIndex<SurveyParticipationIndex>(index => index.Day < before && index.Id > afterId, collection: SurveysConstants.CollectionName)
            .OrderBy(index => index.Id)
            .Take(batchSize)
            .ListAsync(cancellationToken);

        return rows.ToList();
    }

    /// <summary>
    /// Deletes participation records.
    /// </summary>
    /// <param name="ids">The ids of the records.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    public async Task<int> DeleteAsync(IReadOnlyCollection<string> ids, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var count = 0;

        foreach (var row in await _store.GetAsync(ids, cancellationToken))
        {
            await _store.DeleteAsync(row, cancellationToken);
            count++;
        }

        return count;
    }

    private async Task RecordAsync(Survey survey, string respondentKey, RecordRef? context, DateTime utcNow, Action<SurveyParticipation> update)
    {
        if (string.IsNullOrEmpty(respondentKey))
        {
            return;
        }

        // A record per event, never an update of a shared row: a start racing a completion can't lose either.
        var row = new SurveyParticipation
        {
            SurveyId = survey.ItemId,
            RespondentKey = respondentKey,
            ContextKey = ContextKey(survey, context),
            Day = await TodayAsync(utcNow),
        };

        update(row);
        await _store.CreateAsync(row);
    }

    private async Task<DateTime> EndOfDayUtcAsync(DateOnly day)
    {
        var midnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        // A midnight skipped by a daylight-saving change moves forward by the gap: still the start of the next day.
        return DateTime.SpecifyKind(CalendarTime.ToUtc(midnight, await TimeZoneIdAsync()), DateTimeKind.Utc);
    }

    private async Task<DateOnly> TodayAsync(DateTime utcNow)
        => DateOnly.FromDateTime(CalendarTime.ToLocal(utcNow, await TimeZoneIdAsync()));

    // The site's time zone, or the server's when the site has none, as Orchard Core does for the site.
    private async Task<string> TimeZoneIdAsync()
    {
        if (_timeZoneId is null)
        {
            var site = await _siteService.GetSiteSettingsAsync();

            _timeZoneId = !string.IsNullOrEmpty(site?.TimeZoneId) ? site.TimeZoneId : _clock.GetSystemTimeZone()?.TimeZoneId ?? string.Empty;
        }

        return _timeZoneId;
    }

    private static void Add(SurveyParticipationWindowCounts counts, DateOnly day, int value, DateOnly today, DateOnly weekStart, DateOnly monthStart)
    {
        counts.Total += value;

        if (day == today)
        {
            counts.Day += value;
        }

        if (day >= weekStart)
        {
            counts.Week += value;
        }

        if (day >= monthStart)
        {
            counts.Month += value;
        }
    }
}
