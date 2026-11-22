using CrestApps.OrchardCore.Records;
using CrestApps.OrchardCore.Surveys.Core.Access;
using CrestApps.OrchardCore.Surveys.Core.Indexes;
using CrestApps.OrchardCore.Surveys.Core.Services;
using CrestApps.OrchardCore.Surveys.Models;
using CrestApps.OrchardCore.Surveys.Services;
using Microsoft.Extensions.Options;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Surveys.Core.Runtime;

/// <summary>
/// Keeps participation records: how many responses each respondent started and completed each day, without a response
/// id, so limits never link an anonymous respondent to their answers. For an anonymous survey a record also keeps no
/// time finer than the day and only a hash of the context, so it can't be joined to a response by its timestamps; the
/// cooldown of an anonymous survey is then counted from the end of the day of the last completion.
/// </summary>
public sealed class SurveyParticipationService
{
    /// <summary>
    /// How long participation records are kept: longer than the largest limit window.
    /// </summary>
    public const int RetentionDays = 45;

    private readonly SurveyParticipationStore _store;
    private readonly ILocalClock _localClock;
    private readonly ISurveyWeekStartProvider _weekStart;
    private readonly SurveyContextKindOptions _contextKinds;

    /// <summary>
    /// Initializes a new instance of the <see cref="SurveyParticipationService"/> class.
    /// </summary>
    /// <param name="store">The participation store.</param>
    /// <param name="localClock">The local clock.</param>
    /// <param name="weekStart">The first day of the week.</param>
    /// <param name="contextKinds">The registered context kinds.</param>
    public SurveyParticipationService(
        SurveyParticipationStore store,
        ILocalClock localClock,
        ISurveyWeekStartProvider weekStart,
        IOptions<SurveyContextKindOptions> contextKinds)
    {
        _store = store;
        _localClock = localClock;
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
        => RecordAsync(survey, respondentKey, context, utcNow, row => row.StartedCount++);

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
            row.CompletedCount++;
            row.LastCompletedUtc = SurveyAnonymity.IsAnonymous(survey) ? null : utcNow;
        });

    /// <summary>
    /// Deletes participation records older than the retention, a batch at a time.
    /// </summary>
    /// <param name="utcNow">The current time, in UTC.</param>
    /// <param name="batchSize">The most records to delete.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>How many were deleted.</returns>
    public async Task<int> DeleteExpiredAsync(DateTime utcNow, int batchSize, CancellationToken cancellationToken = default)
    {
        var before = (await TodayAsync(utcNow)).AddDays(-RetentionDays).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        var rows = await _store.YesSqlSession.Query<SurveyParticipation, SurveyParticipationIndex>(index => index.Day < before, collection: SurveysConstants.CollectionName)
            .Take(batchSize)
            .ListAsync(cancellationToken);

        var count = 0;

        foreach (var row in rows)
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

        var today = await TodayAsync(utcNow);
        var contextKey = ContextKey(survey, context);
        var row = (await _store.ListAsync(survey.ItemId, respondentKey, today))
            .FirstOrDefault(item => item.Day == today && string.Equals(item.ContextKey ?? string.Empty, contextKey, StringComparison.Ordinal));

        if (row is null)
        {
            row = new SurveyParticipation
            {
                SurveyId = survey.ItemId,
                RespondentKey = respondentKey,
                ContextKey = contextKey,
                Day = today,
            };

            update(row);
            await _store.CreateAsync(row);

            return;
        }

        update(row);
        await _store.UpdateAsync(row);
    }

    private async Task<DateTime> EndOfDayUtcAsync(DateOnly day)
    {
        var midnight = day.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);

        try
        {
            return DateTime.SpecifyKind(await _localClock.ConvertToUtcAsync(midnight), DateTimeKind.Utc);
        }
        catch (ArgumentException)
        {
            // Midnight skipped by a daylight-saving change: an hour later is still the start of the next day.
            return DateTime.SpecifyKind(await _localClock.ConvertToUtcAsync(midnight.AddHours(1)), DateTimeKind.Utc);
        }
    }

    private async Task<DateOnly> TodayAsync(DateTime utcNow)
    {
        var local = await _localClock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utcNow, DateTimeKind.Utc)));

        return DateOnly.FromDateTime(local.DateTime);
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
