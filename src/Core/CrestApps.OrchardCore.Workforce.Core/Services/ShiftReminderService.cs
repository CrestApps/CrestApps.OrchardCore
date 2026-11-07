using System.Globalization;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using CrestApps.OrchardCore.Locking;
using CrestApps.OrchardCore.Workforce.Core.Models;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Workforce.Core.Services;

/// <summary>
/// Reminds workers of their shifts, each as long before it starts as they chose, once per shift and start. A run takes a
/// distributed lock, so two nodes never run at once, and records each reminder before it is sent, so a reminder is never
/// sent twice, even after a restart.
/// </summary>
internal sealed class ShiftReminderService
{
    // The longest reminder a worker can choose, and so how far ahead shifts are read.
    internal const int MaxMinutes = 240;

    internal const string LockKey = "WORKFORCE_SHIFT_REMINDERS";

    private static readonly TimeSpan _lockExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan _retention = TimeSpan.FromDays(2);

    private readonly TeamStore _teamStore;
    private readonly WorkerStore _workerStore;
    private readonly ICalendarQueryService _queryService;
    private readonly IWorkforceNotifier _notifier;
    private readonly IWorkerTimeZoneService _timeZones;
    private readonly ShiftReminderLogStore _logStore;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public ShiftReminderService(
        TeamStore teamStore,
        WorkerStore workerStore,
        ICalendarQueryService queryService,
        IWorkforceNotifier notifier,
        IWorkerTimeZoneService timeZones,
        ShiftReminderLogStore logStore,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IStringLocalizer<ShiftReminderService> stringLocalizer)
    {
        _teamStore = teamStore;
        _workerStore = workerStore;
        _queryService = queryService;
        _notifier = notifier;
        _timeZones = timeZones;
        _logStore = logStore;
        _distributedLock = distributedLock;
        _session = session;
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
        // Another node, or an earlier run that is still going, sends the reminders that are due.
        var (locker, locked) = await _distributedLock.TryAcquireLockWithoutWaitingAsync(LockKey, _lockExpiration);

        if (!locked)
        {
            return 0;
        }

        await using var acquired = locker;

        return await SendUnderLockAsync(cancellationToken);
    }

    private async Task<int> SendUnderLockAsync(CancellationToken cancellationToken)
    {
        var now = _clock.UtcNow;
        var teams = (await _teamStore.ListAsync(cancellationToken)).Where(team => !string.IsNullOrEmpty(team.GetWorkforceSettings().CalendarId)).ToList();

        if (teams.Count == 0)
        {
            return 0;
        }

        var occurrences = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [.. teams.Select(team => team.GetWorkforceSettings().CalendarId).Distinct(StringComparer.Ordinal)],
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
            var key = $"{occurrence.Event.ItemId}:{occurrence.StartUtc.Ticks}";

            if (await _logStore.ExistsAsync(key, cancellationToken))
            {
                continue;
            }

            // Recorded and saved before it is sent, while the lock is held, so the next run, here or on another node,
            // sees it: a reminder can be missed if sending fails, but never sent twice.
            await _logStore.CreateAsync(new ShiftReminderLog
            {
                ItemId = UniqueId.GenerateId(),
                Key = key,
                WorkerId = worker.ItemId,
                SentUtc = now,
            }, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            var zone = await _timeZones.GetAsync(worker, data.TeamId, cancellationToken);
            var start = CalendarTime.ToLocal(occurrence.StartUtc, zone).ToString("t", CultureInfo.CurrentCulture);

            await _notifier.NotifyAsync(new WorkforceNotification
            {
                WorkerId = worker.ItemId,
                Kind = WorkforceNotificationKind.ShiftReminder,
                Title = S["Your shift starts at {0}", start],
                Body = teamNames.TryGetValue(data.TeamId ?? string.Empty, out var team) ? S["With {0}, until {1}.", team, CalendarTime.ToLocal(occurrence.EndUtc, zone).ToString("t", CultureInfo.CurrentCulture)] : null,
                Url = "~/Admin/my-work/schedule",
            }, cancellationToken);
            sent++;
        }

        await _logStore.PurgeAsync(now - _retention, 500, cancellationToken);

        return sent;
    }
}
