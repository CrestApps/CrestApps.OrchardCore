using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Localization;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reminds workers of their shifts, each as long before it starts as they chose, once per shift and start.
/// </summary>
internal sealed class ShiftReminderService
{
    // The longest reminder a worker can choose, and so how far ahead shifts are read.
    internal const int MaxMinutes = 240;

    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly ICalendarQueryService _queryService;
    private readonly IWorkforceNotifier _notifier;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly IDistributedCache _cache;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ShiftReminderService(
        TeamStore teamStore,
        WorkerStore workerStore,
        ICalendarQueryService queryService,
        IWorkforceNotifier notifier,
        IWorkerTimeZoneService timeZones,
        IDistributedCache cache,
        IClock clock,
        IStringLocalizer<ShiftReminderService> stringLocalizer)
    {
        _teamStore = teamStore;
        _workerStore = workerStore;
        _queryService = queryService;
        _notifier = notifier;
        _timeZones = timeZones;
        _cache = cache;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <summary>
    /// Sends the reminders that are due.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the work.</param>
    /// <returns>How many reminders were sent.</returns>
    public async Task<int> SendDueAsync(CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var teams = (await _teamStore.ListAsync(cancellationToken)).Where(team => !string.IsNullOrEmpty(team.CalendarId)).ToList();

        if (teams.Count == 0)
        {
            return 0;
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [.. teams.Select(team => team.CalendarId).Distinct(StringComparer.Ordinal)],
            FromUtc = now,
            ToUtc = now.AddMinutes(MaxMinutes),
            EventTypes = [WorkforceConstants.EventTypes.Shift],
        }, cancellationToken);
        var teamNames = teams.ToDictionary(team => team.ItemId, team => team.Name, StringComparer.Ordinal);
        var workers = new Dictionary<string, WorkerProfile>(StringComparer.Ordinal);
        var sent = 0;

        foreach (var occurrence in occurrences.Where(occurrence => occurrence.StartUtc > now))
        {
            if (!ShiftEvents.TryRead(occurrence.Event, out var data) || string.IsNullOrEmpty(data.WorkerId))
            {
                continue;
            }

            if (!workers.TryGetValue(data.WorkerId, out var worker))
            {
                worker = await _workerStore.FindByIdAsync(data.WorkerId, cancellationToken);
                workers[data.WorkerId] = worker;
            }

            var minutes = worker?.TryGet<WorkerPortalSettings>(out var settings) == true ? settings.ReminderMinutes : new WorkerPortalSettings().ReminderMinutes;

            if (worker is null || worker.Status != WorkerStatus.Active || minutes <= 0 || occurrence.StartUtc.AddMinutes(-Math.Min(minutes, MaxMinutes)) > now)
            {
                continue;
            }

            // Once per shift and start: a moved shift is reminded again at its new time.
            var key = $"workforce-shift-reminder:{occurrence.Event.ItemId}:{occurrence.StartUtc.Ticks}";

            if (await _cache.GetAsync(key, cancellationToken) is not null)
            {
                continue;
            }

            await _cache.SetAsync(key, [1], new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(2) }, cancellationToken);

            var zone = await _timeZones.GetAsync(worker, data.TeamId, cancellationToken);
            var start = CalendarTime.ToLocal(occurrence.StartUtc, zone).ToString("t", CultureInfo.CurrentCulture);

            await _notifier.NotifyAsync(new WorkforceNotification
            {
                WorkerId = worker.ItemId,
                Kind = WorkforceNotificationKind.ShiftReminder,
                Title = S["Your shift starts at {0}", start],
                Body = teamNames.TryGetValue(data.TeamId ?? string.Empty, out var team) ? S["With {0}, until {1}.", team, CalendarTime.ToLocal(occurrence.EndUtc, zone).ToString("t", CultureInfo.CurrentCulture)] : null,
                Url = "~/my-work/schedule",
            }, cancellationToken);
            sent++;
        }

        return sent;
    }
}
