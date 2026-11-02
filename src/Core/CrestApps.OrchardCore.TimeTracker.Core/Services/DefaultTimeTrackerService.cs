using System.Globalization;
using CrestApps.OrchardCore.TimeTracker.Core.Indexes;
using CrestApps.OrchardCore.TimeTracker.Models;
using CrestApps.OrchardCore.TimeTracker.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using NodaTime;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using YesSql;
using YesSql.Services;
using IClock = OrchardCore.Modules.IClock;

namespace CrestApps.OrchardCore.TimeTracker.Core.Services;

/// <summary>
/// Tracks people's time, under a lock per person, and totals it by category.
/// </summary>
public sealed class DefaultTimeTrackerService : ITimeTrackerService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly TimeEntryStore _entries;
    private readonly TimeStageStore _stages;
    private readonly TimeCategoryStore _categories;
    private readonly ISession _session;
    private readonly IDistributedLock _distributedLock;
    private readonly IEnumerable<ITimeEntryObserver> _observers;
    private readonly IEnumerable<global::OrchardCore.Modules.ILocalClock> _localClocks;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultTimeTrackerService"/> class.
    /// </summary>
    public DefaultTimeTrackerService(
        TimeEntryStore entries,
        TimeStageStore stages,
        TimeCategoryStore categories,
        ISession session,
        IDistributedLock distributedLock,
        IEnumerable<ITimeEntryObserver> observers,
        IEnumerable<global::OrchardCore.Modules.ILocalClock> localClocks,
        IClock clock,
        ILogger<DefaultTimeTrackerService> logger,
        IStringLocalizer<DefaultTimeTrackerService> stringLocalizer)
    {
        _entries = entries;
        _stages = stages;
        _categories = categories;
        _session = session;
        _distributedLock = distributedLock;
        _observers = observers;
        _localClocks = localClocks;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerStatus> GetStatusAsync(string userId, CancellationToken cancellationToken = default)
        => new() { UserId = userId, Open = await OpenEntryAsync(userId, cancellationToken) };

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> SwitchAsync(string userId, string userName, string stageId, string source, string actor, string sourceRef = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return TimeTrackerResult.Fail(S["Choose whose time it is."]);
        }

        var stage = await FindStageAsync(stageId, cancellationToken);

        if (stage is null)
        {
            return TimeTrackerResult.Fail(S["Choose a stage that is in use."]);
        }

        await using var locker = await LockAsync(userId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var now = Trim(_clock.UtcNow);
        var open = await OpenEntryAsync(userId, cancellationToken);

        if (open?.StageId == stage.ItemId)
        {
            return new TimeTrackerResult { Entry = open };
        }

        if (open is not null)
        {
            open.EndUtc = now < open.StartUtc ? open.StartUtc : now;
            await _entries.UpdateAsync(open, cancellationToken);
        }

        var entry = new TimeEntry
        {
            ItemId = CrestApps.Core.UniqueId.GenerateId(),
            UserId = userId,
            UserName = userName ?? open?.UserName,
            StageId = stage.ItemId,
            StartUtc = open?.EndUtc ?? now,
            Source = source ?? TimeTrackerConstants.Sources.Self,
            SourceRef = sourceRef,
            CreatedBy = actor,
            CreatedUtc = now,
        };

        await _entries.CreateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        if (open is not null)
        {
            await NotifyAsync(TimeEntryChangeKind.Ended, open, null, actor, null, cancellationToken);
        }

        await NotifyAsync(TimeEntryChangeKind.Created, entry, null, actor, null, cancellationToken);

        return new TimeTrackerResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> ClockOutAsync(string userId, string actor, CancellationToken cancellationToken = default)
    {
        await using var locker = await LockAsync(userId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var open = await OpenEntryAsync(userId, cancellationToken);

        if (open is null)
        {
            return TimeTrackerResult.Fail(S["Not clocked in."]);
        }

        var now = Trim(_clock.UtcNow);

        open.EndUtc = now < open.StartUtc ? open.StartUtc : now;
        await _entries.UpdateAsync(open, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await NotifyAsync(TimeEntryChangeKind.Ended, open, null, actor, null, cancellationToken);

        return new TimeTrackerResult { Entry = open };
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> AddAsync(TimeEntry entry, string actor, string reason, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (string.IsNullOrEmpty(entry.UserId))
        {
            return TimeTrackerResult.Fail(S["Choose whose time it is."]);
        }

        if (entry.EndUtc is null)
        {
            return TimeTrackerResult.Fail(S["An added entry needs its end; to start one now, switch stage."]);
        }

        await using var locker = await LockAsync(entry.UserId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        entry.ItemId ??= CrestApps.Core.UniqueId.GenerateId();
        entry.StartUtc = Trim(entry.StartUtc);
        entry.EndUtc = Trim(entry.EndUtc.Value);
        entry.Source ??= TimeTrackerConstants.Sources.Manager;
        entry.CreatedBy ??= actor;
        entry.CreatedUtc = _clock.UtcNow;

        var errors = await ValidateAsync(entry, cancellationToken);

        if (errors.Count > 0)
        {
            return new TimeTrackerResult { Entry = entry, Errors = errors };
        }

        if (!string.IsNullOrWhiteSpace(reason))
        {
            entry.Note ??= reason.Trim();
        }

        await _entries.CreateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await NotifyAsync(TimeEntryChangeKind.Created, entry, null, actor, reason, cancellationToken);

        return new TimeTrackerResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> UpdateAsync(string entryId, string stageId, DateTime startUtc, DateTime? endUtc, string note, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return TimeTrackerResult.Fail(S["Say why the time is changed."]);
        }

        var found = await FindAsync(entryId, cancellationToken);

        if (found is null)
        {
            return TimeTrackerResult.Fail(S["The entry doesn't exist."]);
        }

        await using var locker = await LockAsync(found.UserId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var entry = await FindAsync(entryId, cancellationToken);
        var before = await DescribeAsync(entry, cancellationToken);

        if (entry.EndUtc is not null && endUtc is null)
        {
            return TimeTrackerResult.Fail(S["An entry that has ended needs its end."]);
        }

        entry.StageId = stageId;
        entry.StartUtc = Trim(startUtc);
        entry.EndUtc = endUtc is null ? null : Trim(endUtc.Value);
        entry.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();

        var errors = await ValidateAsync(entry, cancellationToken);

        if (errors.Count > 0)
        {
            return new TimeTrackerResult { Entry = entry, Errors = errors };
        }

        entry.Changes.Add(new TimeEntryChange { Utc = _clock.UtcNow, By = actor, Reason = reason.Trim(), Before = before });
        await _entries.UpdateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await NotifyAsync(TimeEntryChangeKind.Edited, entry, before, actor, reason.Trim(), cancellationToken);

        return new TimeTrackerResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> DeleteAsync(string entryId, string actor, string reason, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return TimeTrackerResult.Fail(S["Say why the time is removed."]);
        }

        var entry = await FindAsync(entryId, cancellationToken);

        if (entry is null)
        {
            return TimeTrackerResult.Fail(S["The entry doesn't exist."]);
        }

        var before = await DescribeAsync(entry, cancellationToken);

        await _entries.DeleteAsync(entry, cancellationToken);
        await NotifyAsync(TimeEntryChangeKind.Deleted, entry, before, actor, reason.Trim(), cancellationToken);

        return new TimeTrackerResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimeEntry> FindAsync(string entryId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(entryId) ? null : await _entries.FindByIdAsync(entryId, cancellationToken);

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimeEntry>> ListAsync(TimeEntryQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var search = _session.Query<TimeEntry, TimeEntryIndex>(collection: TimeTrackerConstants.CollectionName);

        if (query.UserIds is { } userIds)
        {
            var ids = userIds.ToArray();

            search = search.Where(index => index.UserId.IsIn(ids));
        }

        if (!string.IsNullOrEmpty(query.StageId))
        {
            search = search.Where(index => index.StageId == query.StageId);
        }

        if (query.FromUtc is { } from)
        {
            search = search.Where(index => index.EndUtc > from);
        }

        if (query.ToUtc is { } to)
        {
            search = search.Where(index => index.StartUtc < to);
        }

        return [.. await search.OrderBy(index => index.StartUtc).ListAsync(cancellationToken)];
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<TimeTotalsRow>> GetTotalsAsync(TimeEntryQuery query, string timeZoneId, bool byDay, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entries = await ListAsync(query, cancellationToken);
        var stages = (await _stages.GetAllAsync(cancellationToken)).ToDictionary(stage => stage.ItemId, StringComparer.Ordinal);
        var factors = (await _categories.GetAllAsync(cancellationToken)).Where(category => category.Enabled).ToDictionary(category => category.ItemId, category => category.Factor, StringComparer.Ordinal);
        var zone = DateTimeZoneProviders.Tzdb.GetZoneOrNull(timeZoneId ?? string.Empty) ?? DateTimeZone.Utc;
        var now = _clock.UtcNow;
        var rows = new Dictionary<(string UserId, DateOnly? Date), TimeTotalsRow>();

        foreach (var entry in entries)
        {
            var start = query.FromUtc is { } from && from > entry.StartUtc ? from : entry.StartUtc;
            var end = entry.EndUtc ?? now;

            if (query.ToUtc is { } to && to < end)
            {
                end = to;
            }

            foreach (var (pieceStart, pieceEnd, date) in Pieces(start, end, zone, byDay))
            {
                var minutes = (int)Math.Round((pieceEnd - pieceStart).TotalMinutes);

                if (minutes <= 0)
                {
                    continue;
                }

                var key = (entry.UserId, date);

                if (!rows.TryGetValue(key, out var row))
                {
                    row = new TimeTotalsRow { UserId = entry.UserId, UserName = entry.UserName, Date = date };
                    rows[key] = row;
                }

                row.TotalMinutes += minutes;
                row.MinutesByStage[entry.StageId ?? string.Empty] = row.MinutesByStage.GetValueOrDefault(entry.StageId ?? string.Empty) + minutes;

                if (entry.StageId is not null && stages.TryGetValue(entry.StageId, out var stage))
                {
                    foreach (var categoryId in stage.CategoryIds.Distinct(StringComparer.Ordinal))
                    {
                        if (factors.TryGetValue(categoryId, out var factor))
                        {
                            row.MinutesByCategory[categoryId] = row.MinutesByCategory.GetValueOrDefault(categoryId) + (minutes * factor);
                        }
                    }
                }
            }
        }

        return [.. rows.Values.OrderBy(row => row.UserName, StringComparer.CurrentCultureIgnoreCase).ThenBy(row => row.Date)];
    }

    // Splits a span at local midnights when totals are by day.
    private static IEnumerable<(DateTime Start, DateTime End, DateOnly? Date)> Pieces(DateTime start, DateTime end, DateTimeZone zone, bool byDay)
    {
        if (!byDay)
        {
            yield return (start, end, null);

            yield break;
        }

        var cursor = start;

        while (cursor < end)
        {
            var local = Instant.FromDateTimeUtc(DateTime.SpecifyKind(cursor, DateTimeKind.Utc)).InZone(zone);
            var date = local.Date;
            var nextMidnight = zone.AtStartOfDay(date.PlusDays(1)).ToDateTimeUtc();
            var pieceEnd = nextMidnight < end ? nextMidnight : end;

            yield return (cursor, pieceEnd, new DateOnly(date.Year, date.Month, date.Day));

            cursor = pieceEnd;
        }
    }

    private async Task<List<string>> ValidateAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        if (await FindStageAsync(entry.StageId, cancellationToken, includeDisabled: true) is null)
        {
            errors.Add(S["Choose a stage."]);
        }

        if (entry.EndUtc is { } end && end <= entry.StartUtc)
        {
            errors.Add(S["The entry must end after it starts."]);
        }

        if (entry.StartUtc > _clock.UtcNow.AddMinutes(1))
        {
            errors.Add(S["Time can't be recorded in the future."]);
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        var end2 = entry.EndUtc ?? TimeTrackerIndexProvider.OpenEnd;
        var overlapping = await _session.QueryIndex<TimeEntryIndex>(index => index.UserId == entry.UserId && index.ItemId != entry.ItemId && index.StartUtc < end2 && index.EndUtc > entry.StartUtc, collection: TimeTrackerConstants.CollectionName)
            .FirstOrDefaultAsync(cancellationToken);

        if (overlapping is not null)
        {
            errors.Add(S["It overlaps other time from {0} to {1}.", await LocalAsync(overlapping.StartUtc), overlapping.IsOpen ? S["now"].Value : await LocalAsync(overlapping.EndUtc)]);
        }

        return errors;
    }

    // In the site's time when there is a site; tests and tools without one get UTC.
    private async Task<string> LocalAsync(DateTime utc)
    {
        var local = _localClocks.FirstOrDefault() is { } clock
            ? (await clock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime
            : utc;

        return local.ToString("ddd d MMM HH:mm:ss", CultureInfo.CurrentCulture);
    }

    private async Task<string> DescribeAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        var stage = await FindStageAsync(entry.StageId, cancellationToken, includeDisabled: true);

        return string.Create(CultureInfo.InvariantCulture, $"{stage?.Name ?? entry.StageId} {entry.StartUtc:yyyy-MM-dd HH:mm}-{(entry.EndUtc is { } end ? end.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : "open")} UTC");
    }

    private async Task<TimeStage> FindStageAsync(string stageId, CancellationToken cancellationToken, bool includeDisabled = false)
    {
        var stage = string.IsNullOrEmpty(stageId) ? null : await _stages.FindByIdAsync(stageId, cancellationToken);

        return stage is not null && (includeDisabled || stage.Enabled) ? stage : null;
    }

    private async Task<TimeEntry> OpenEntryAsync(string userId, CancellationToken cancellationToken)
        => string.IsNullOrEmpty(userId)
            ? null
            : await _session.Query<TimeEntry, TimeEntryIndex>(index => index.UserId == userId && index.IsOpen, collection: TimeTrackerConstants.CollectionName).FirstOrDefaultAsync(cancellationToken);

    private async Task<Locker> LockAsync(string userId)
    {
        var (locker, locked) = await _distributedLock.TryAcquireLockAsync(TimeTrackerConstants.UserLockPrefix + userId, _lockTimeout, _lockExpiration);

        return locked ? new Locker(locker) : null;
    }

    private async Task NotifyAsync(TimeEntryChangeKind kind, TimeEntry entry, string before, string actor, string reason, CancellationToken cancellationToken)
    {
        foreach (var observer in _observers)
        {
            try
            {
                await observer.OnChangedAsync(kind, entry, before, actor, reason, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                _logger.LogError(exception, "A time entry observer failed on {Kind}.", kind);
            }
        }
    }

    private static DateTime Trim(DateTime value)
        => new(value.Ticks - (value.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);

    private sealed class Locker : IAsyncDisposable
    {
        private readonly ILocker _locker;

        public Locker(ILocker locker)
        {
            _locker = locker;
        }

        public ValueTask DisposeAsync() => _locker.DisposeAsync();
    }
}
