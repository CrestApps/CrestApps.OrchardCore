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
    private readonly IEnumerable<ITimeEntryChargeResolver> _chargeResolvers;
    private readonly IEnumerable<IExternalTimeClock> _externalClocks;
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
        IEnumerable<ITimeEntryChargeResolver> chargeResolvers,
        IEnumerable<IExternalTimeClock> externalClocks,
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
        _chargeResolvers = chargeResolvers;
        _externalClocks = externalClocks;
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

        if (await ExternalClockAsync(userId, cancellationToken) is { } keeper)
        {
            return TimeTrackerResult.Fail(S["Your time is kept by {0}.", keeper.Name]);
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

        // The same stage for something else, such as another ticket, is a new entry.
        if (open?.StageId == stage.ItemId && string.Equals(open.SourceRef, sourceRef, StringComparison.Ordinal))
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

        await ChargeAsync(entry, cancellationToken);
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

        if (open.ManagedBy is not null && await ExternalClockAsync(userId, cancellationToken) is { } keeper)
        {
            return TimeTrackerResult.Fail(S["Your time is kept by {0}.", keeper.Name]);
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

        await ChargeAsync(entry, cancellationToken);
        await _entries.CreateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);
        await NotifyAsync(TimeEntryChangeKind.Created, entry, null, actor, reason, cancellationToken);

        return new TimeTrackerResult { Entry = entry };
    }

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> InsertAsync(TimeEntry entry, string actor, string reason, CancellationToken cancellationToken = default)
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

        if (await ExternalClockAsync(entry.UserId, cancellationToken) is { } keeper)
        {
            return TimeTrackerResult.Fail(S["Your time is kept by {0}.", keeper.Name]);
        }

        await using var locker = await LockAsync(entry.UserId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var now = _clock.UtcNow;

        entry.ItemId ??= CrestApps.Core.UniqueId.GenerateId();
        entry.StartUtc = Trim(entry.StartUtc);
        entry.EndUtc = Trim(entry.EndUtc.Value);
        entry.Source ??= TimeTrackerConstants.Sources.Manager;
        entry.CreatedBy ??= actor;
        entry.CreatedUtc = now;

        var errors = new List<string>();

        if (await FindStageAsync(entry.StageId, cancellationToken) is null)
        {
            errors.Add(S["Choose a stage that is in use."]);
        }

        if (entry.EndUtc <= entry.StartUtc)
        {
            errors.Add(S["The entry must end after it starts."]);
        }
        else if (entry.EndUtc > now.AddMinutes(1))
        {
            errors.Add(S["Time can't be recorded in the future."]);
        }

        if (errors.Count > 0)
        {
            return new TimeTrackerResult { Entry = entry, Errors = errors };
        }

        var start = entry.StartUtc;
        var end = entry.EndUtc.Value;
        var covered = (await _session.Query<TimeEntry, TimeEntryIndex>(index => index.UserId == entry.UserId && index.StartUtc < end && index.EndUtc > start, collection: TimeTrackerConstants.CollectionName)
            .ListAsync(cancellationToken)).ToList();

        // Time another clock keeps is corrected there, and time of the same source (another record's) is never moved.
        if (covered.FirstOrDefault(other => other.ManagedBy is not null) is { } kept)
        {
            return await KeptElsewhereAsync(kept, cancellationToken);
        }

        if (covered.FirstOrDefault(other => string.Equals(other.Source, entry.Source, StringComparison.Ordinal)) is { } same)
        {
            return TimeTrackerResult.Fail(S["It overlaps other time from {0} to {1}.", await LocalAsync(same.StartUtc), same.IsOpen ? S["now"].Value : await LocalAsync(same.EndUtc.Value)]);
        }

        var open = TimeTrackerIndexProvider.OpenEnd;
        var edited = new List<(TimeEntry Entry, string Before)>();
        var created = new List<TimeEntry>();
        var deleted = new List<TimeEntry>();

        foreach (var other in covered)
        {
            var pieces = Subtract(other.StartUtc, other.EndUtc ?? open, [(start, end)]).ToList();

            if (pieces.Count == 0)
            {
                await _entries.DeleteAsync(other, cancellationToken);
                deleted.Add(other);

                continue;
            }

            var before = await DescribeAsync(other, cancellationToken);

            other.StartUtc = pieces[0].Start;
            other.EndUtc = pieces[0].End == open ? null : pieces[0].End;
            other.Changes.Add(new TimeEntryChange { Utc = now, By = actor, Reason = reason, Before = before });
            await _entries.UpdateAsync(other, cancellationToken);
            edited.Add((other, before));

            if (pieces.Count > 1)
            {
                var rest = new TimeEntry
                {
                    ItemId = CrestApps.Core.UniqueId.GenerateId(),
                    UserId = other.UserId,
                    UserName = other.UserName,
                    StageId = other.StageId,
                    StartUtc = pieces[1].Start,
                    EndUtc = pieces[1].End == open ? null : pieces[1].End,
                    Source = other.Source,
                    SourceRef = other.SourceRef,
                    ChargedTo = other.ChargedTo,
                    Note = other.Note,
                    CreatedBy = actor,
                    CreatedUtc = now,
                };

                await _entries.CreateAsync(rest, cancellationToken);
                created.Add(rest);
            }
        }

        await ChargeAsync(entry, cancellationToken);
        await _entries.CreateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        foreach (var (changed, before) in edited)
        {
            await NotifyAsync(TimeEntryChangeKind.Edited, changed, before, actor, reason, cancellationToken);
        }

        foreach (var removed in deleted)
        {
            await NotifyAsync(TimeEntryChangeKind.Deleted, removed, null, actor, reason, cancellationToken);
        }

        foreach (var rest in created)
        {
            await NotifyAsync(TimeEntryChangeKind.Created, rest, null, actor, reason, cancellationToken);
        }

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

        if (await KeptElsewhereAsync(found, cancellationToken) is { } kept)
        {
            return kept;
        }

        await using var locker = await LockAsync(found.UserId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var entry = await FindAsync(entryId, cancellationToken);

        if (!string.IsNullOrEmpty(entry.BilledInvoiceId))
        {
            return TimeTrackerResult.Fail(S["This time is billed on an invoice, so it can't be changed. Take it off the invoice first."]);
        }

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
    public async Task<TimeTrackerResult> SetBilledAsync(string entryId, string invoiceId, string lineId, CancellationToken cancellationToken = default)
    {
        var entry = string.IsNullOrEmpty(entryId) ? null : await FindAsync(entryId, cancellationToken);

        if (entry is null)
        {
            return TimeTrackerResult.Fail(S["The entry doesn't exist."]);
        }

        if (!string.IsNullOrEmpty(invoiceId))
        {
            if (entry.IsOpen)
            {
                return TimeTrackerResult.Fail(S["Time still running can't be billed."]);
            }

            if (!string.IsNullOrEmpty(entry.BilledInvoiceId) && !string.Equals(entry.BilledInvoiceId, invoiceId, StringComparison.Ordinal))
            {
                return TimeTrackerResult.Fail(S["This time is billed on another invoice already."]);
            }
        }

        entry.BilledInvoiceId = string.IsNullOrEmpty(invoiceId) ? null : invoiceId;
        entry.BilledLineId = string.IsNullOrEmpty(invoiceId) ? null : lineId;

        await _entries.UpdateAsync(entry, cancellationToken);

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

        if (await KeptElsewhereAsync(entry, cancellationToken) is { } kept)
        {
            return kept;
        }

        if (!string.IsNullOrEmpty(entry.BilledInvoiceId))
        {
            return TimeTrackerResult.Fail(S["This time is billed on an invoice, so it can't be removed. Take it off the invoice first."]);
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

        if (!string.IsNullOrEmpty(query.SourceRef))
        {
            search = search.Where(index => index.SourceRef == query.SourceRef);
        }

        if (!string.IsNullOrEmpty(query.Source))
        {
            search = search.Where(index => index.Source == query.Source);
        }

        if (query.SourceRefs is { } sourceRefs)
        {
            var refs = sourceRefs.ToArray();

            search = search.Where(index => index.SourceRef.IsIn(refs));
        }

        if (query.ChargedTo is { } chargedTo)
        {
            var charged = chargedTo.ToString();

            search = search.Where(index => index.ChargedTo == charged);
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

    /// <inheritdoc/>
    public async Task<TimeTrackerResult> SyncAsync(TimeEntrySync sync, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sync);

        if (string.IsNullOrEmpty(sync.UserId) || string.IsNullOrEmpty(sync.ManagedBy) || sync.BuildAsync is null)
        {
            return TimeTrackerResult.Fail(S["Choose whose time it is and the clock that keeps it."]);
        }

        await using var locker = await LockAsync(sync.UserId);

        if (locker is null)
        {
            return TimeTrackerResult.Fail(S["Someone else is changing this time. Try again."]);
        }

        var open = TimeTrackerIndexProvider.OpenEnd;
        var now = _clock.UtcNow;
        var desired = (await sync.BuildAsync(cancellationToken) ?? [])
            .Where(entry => !string.IsNullOrEmpty(entry.StageId) && entry.StartUtc <= now && (entry.EndUtc ?? open) > sync.FromUtc && entry.StartUtc < sync.ToUtc)
            .Select(entry => (Entry: entry, Start: Trim(sync.ReplacesRangeOnly && entry.StartUtc < sync.FromUtc ? sync.FromUtc : entry.StartUtc), End: entry.EndUtc is { } end ? Trim(end) : open))
            .Where(piece => piece.End > piece.Start)
            .OrderBy(piece => piece.Start)
            .ToList();
        var overrides = new HashSet<string>(sync.Overrides ?? [], StringComparer.Ordinal);

        // Read everything the new entries could touch, not only the range, so they are cut around all other time.
        var from = desired.Count > 0 && desired[0].Start < sync.FromUtc ? desired[0].Start : sync.FromUtc;
        var last = desired.Count > 0 ? desired.Max(piece => piece.End) : sync.ToUtc;
        var to = last > sync.ToUtc ? last : sync.ToUtc;
        var existing = await _session.Query<TimeEntry, TimeEntryIndex>(index => index.UserId == sync.UserId && index.StartUtc < to && index.EndUtc > from, collection: TimeTrackerConstants.CollectionName)
            .ListAsync(cancellationToken);
        var unused = existing.Where(entry => entry.ManagedBy == sync.ManagedBy).ToList();
        var overridden = existing.Where(entry => entry.ManagedBy is not null && overrides.Contains(entry.ManagedBy)).ToList();
        var ended = new List<TimeEntry>();
        var kept = new List<(DateTime Start, DateTime End)>();

        // A sync of a range keeps what it kept before the range: an entry running into it ends at its start, or goes
        // on when the range starts in the same stage.
        if (sync.ReplacesRangeOnly)
        {
            foreach (var before in unused.Where(entry => entry.StartUtc < sync.FromUtc).ToList())
            {
                unused.Remove(before);

                var next = desired.FirstOrDefault(piece => piece.Start == sync.FromUtc);

                if (next.Entry is not null && next.Entry.StageId == before.StageId && next.Entry.SourceRef == before.SourceRef)
                {
                    desired.Remove(next);
                    before.EndUtc = next.End == open ? null : next.End;
                }
                else
                {
                    before.EndUtc = sync.FromUtc;
                }

                await _entries.UpdateAsync(before, cancellationToken);
                kept.Add((before.StartUtc, before.EndUtc ?? open));
            }
        }

        foreach (var left in existing.Where(entry => entry.ManagedBy != sync.ManagedBy && !overridden.Contains(entry) && entry.EndUtc is null))
        {
            if (desired.FirstOrDefault(piece => piece.Start > left.StartUtc) is { Entry: not null } next)
            {
                left.EndUtc = next.Start;
                await _entries.UpdateAsync(left, cancellationToken);
                ended.Add(left);
            }
        }

        var others = existing.Where(entry => entry.ManagedBy != sync.ManagedBy && !overridden.Contains(entry)).Select(entry => (Start: entry.StartUtc, End: entry.EndUtc ?? open)).OrderBy(span => span.Start).ToList();

        foreach (var (entry, start, end) in desired)
        {
            foreach (var (pieceStart, pieceEnd) in Subtract(start, end, others))
            {
                kept.Add((pieceStart, pieceEnd));

                DateTime? pieceEndUtc = pieceEnd == open ? null : pieceEnd;
                var same = unused.FirstOrDefault(old => old.StageId == entry.StageId && old.StartUtc == pieceStart && old.EndUtc == pieceEndUtc && old.SourceRef == entry.SourceRef);

                if (same is not null)
                {
                    unused.Remove(same);

                    continue;
                }

                var synced = new TimeEntry
                {
                    ItemId = CrestApps.Core.UniqueId.GenerateId(),
                    UserId = sync.UserId,
                    UserName = sync.UserName ?? entry.UserName,
                    StageId = entry.StageId,
                    StartUtc = pieceStart,
                    EndUtc = pieceEndUtc,
                    Source = entry.Source ?? sync.Source ?? TimeTrackerConstants.Sources.Api,
                    SourceRef = entry.SourceRef,
                    ChargedTo = entry.ChargedTo,
                    Note = entry.Note,
                    ManagedBy = sync.ManagedBy,
                    CreatedBy = entry.CreatedBy ?? sync.ManagedBy,
                    CreatedUtc = now,
                };

                await ChargeAsync(synced, cancellationToken);
                await _entries.CreateAsync(synced, cancellationToken);
            }
        }

        foreach (var old in unused)
        {
            await _entries.DeleteAsync(old, cancellationToken);
        }

        // The time of a clock this one overrides is cut around this clock's, which that clock does too when it syncs.
        var ours = kept.OrderBy(span => span.Start).ToList();

        foreach (var other in overridden)
        {
            var pieces = Subtract(other.StartUtc, other.EndUtc ?? open, ours).ToList();

            if (pieces.Count == 1 && pieces[0].Start == other.StartUtc && pieces[0].End == (other.EndUtc ?? open))
            {
                continue;
            }

            if (pieces.Count == 0)
            {
                await _entries.DeleteAsync(other, cancellationToken);

                continue;
            }

            other.StartUtc = pieces[0].Start;
            other.EndUtc = pieces[0].End == open ? null : pieces[0].End;
            await _entries.UpdateAsync(other, cancellationToken);

            foreach (var (pieceStart, pieceEnd) in pieces.Skip(1))
            {
                await _entries.CreateAsync(new TimeEntry
                {
                    ItemId = CrestApps.Core.UniqueId.GenerateId(),
                    UserId = other.UserId,
                    UserName = other.UserName,
                    StageId = other.StageId,
                    StartUtc = pieceStart,
                    EndUtc = pieceEnd == open ? null : pieceEnd,
                    Source = other.Source,
                    SourceRef = other.SourceRef,
                    ChargedTo = other.ChargedTo,
                    Note = other.Note,
                    ManagedBy = other.ManagedBy,
                    CreatedBy = other.CreatedBy,
                    CreatedUtc = now,
                }, cancellationToken);
            }
        }

        await _session.SaveChangesAsync(cancellationToken);

        foreach (var left in ended)
        {
            await NotifyAsync(TimeEntryChangeKind.Ended, left, null, sync.ManagedBy, null, cancellationToken);
        }

        return new TimeTrackerResult();
    }

    // The parts of a span that other time doesn't cover; the other spans are sorted by start.
    private static IEnumerable<(DateTime Start, DateTime End)> Subtract(DateTime start, DateTime end, List<(DateTime Start, DateTime End)> others)
    {
        var cursor = start;

        foreach (var other in others)
        {
            if (other.End <= cursor || other.Start >= end)
            {
                continue;
            }

            if (other.Start > cursor)
            {
                yield return (cursor, other.Start);
            }

            cursor = other.End;

            if (cursor >= end)
            {
                yield break;
            }
        }

        if (cursor < end)
        {
            yield return (cursor, end);
        }
    }

    /// <summary>
    /// Asks the charge resolvers what a new entry counts against, unless it already says. Called inside the lock and
    /// before the save that creates the entry, so the charge is the one in force when the time was logged.
    /// </summary>
    /// <param name="entry">The entry being created.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    internal async Task ChargeAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        if (!entry.ChargedTo.IsEmpty || string.IsNullOrEmpty(entry.SourceRef))
        {
            return;
        }

        foreach (var resolver in _chargeResolvers)
        {
            var chargedTo = await resolver.ResolveAsync(entry, cancellationToken);

            if (!chargedTo.IsEmpty)
            {
                entry.ChargedTo = chargedTo;

                return;
            }
        }
    }

    private async Task<ExternalTimeClockInfo> ExternalClockAsync(string userId, CancellationToken cancellationToken)
    {
        foreach (var clock in _externalClocks)
        {
            if (await clock.FindAsync(userId, cancellationToken) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    // A clock that keeps an entry corrects it itself, then syncs it again.
    private async Task<TimeTrackerResult> KeptElsewhereAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(entry.ManagedBy))
        {
            return null;
        }

        string name = null;

        foreach (var clock in _externalClocks)
        {
            if (await clock.FindByIdAsync(entry.ManagedBy, cancellationToken) is { } found)
            {
                name = found.Name;

                break;
            }
        }

        return TimeTrackerResult.Fail(S["This time comes from {0}; correct it there.", name ?? entry.ManagedBy]);
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

    private async Task<string> LocalAsync(DateTime utc)
        => (await ToLocalAsync(utc)).ToString("ddd d MMM HH:mm:ss", CultureInfo.CurrentCulture);

    // In the site's time when there is a site; tests and tools without one get UTC.
    private async Task<DateTime> ToLocalAsync(DateTime utc)
        => _localClocks.FirstOrDefault() is { } clock
            ? (await clock.ConvertToLocalAsync(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)))).DateTime
            : utc;

    private async Task<string> DescribeAsync(TimeEntry entry, CancellationToken cancellationToken)
    {
        var stage = await FindStageAsync(entry.StageId, cancellationToken, includeDisabled: true);
        var start = (await ToLocalAsync(entry.StartUtc)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        var end = entry.EndUtc is { } endUtc
            ? (await ToLocalAsync(endUtc)).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture)
            : "open";

        return $"{stage?.Name ?? entry.StageId} {start}-{end}";
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
