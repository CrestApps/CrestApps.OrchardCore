using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core.Recurrence;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using OrchardCore.Settings;

namespace CrestApps.OrchardCore.Calendars.Core.Services;

/// <summary>
/// Creates, changes, cancels and finds events. Validation and the overlap limit run on every write; the overlap limit is checked
/// under a distributed lock and guarded by a concurrency-checked version on each limited calendar, so two requests can never
/// both take the last place even across servers.
/// </summary>
public sealed class DefaultCalendarEventManager : ICalendarEventManager
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly ICalendarEventStore _eventStore;
    private readonly ICalendarStore _calendarStore;
    private readonly ICalendarTypeManager _typeManager;
    private readonly IEnumerable<ICalendarEventHandler> _handlers;
    private readonly CalendarChangeQueue _changes;
    private readonly CalendarConflictChecker _conflictChecker;
    private readonly IDistributedLock _distributedLock;
    private readonly ISiteService _siteService;
    private readonly IClock _clock;
    private readonly ILogger _logger;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultCalendarEventManager"/> class.
    /// </summary>
    /// <param name="eventStore">The event store.</param>
    /// <param name="calendarStore">The calendar store.</param>
    /// <param name="typeManager">The calendar type manager.</param>
    /// <param name="handlers">The event handlers.</param>
    /// <param name="changes">The queue of changes for the after-commit observers.</param>
    /// <param name="conflictChecker">The overlap checker.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="siteService">The site service, for the site's time zone.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultCalendarEventManager(
        ICalendarEventStore eventStore,
        ICalendarStore calendarStore,
        ICalendarTypeManager typeManager,
        IEnumerable<ICalendarEventHandler> handlers,
        CalendarChangeQueue changes,
        CalendarConflictChecker conflictChecker,
        IDistributedLock distributedLock,
        ISiteService siteService,
        IClock clock,
        ILogger<DefaultCalendarEventManager> logger,
        IStringLocalizer<DefaultCalendarEventManager> stringLocalizer)
    {
        _eventStore = eventStore;
        _calendarStore = calendarStore;
        _typeManager = typeManager;
        _handlers = handlers;
        _changes = changes;
        _conflictChecker = conflictChecker;
        _distributedLock = distributedLock;
        _siteService = siteService;
        _clock = clock;
        _logger = logger;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<CalendarEvent> NewAsync(Calendar calendar, string eventType = null, DateTime? startUtc = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendar);

        var descriptor = _typeManager.FindEventType(calendar.Type, eventType) ?? _typeManager.FindEventType(calendar.Type, null);
        var timeZoneId = await GetTimeZoneIdAsync(calendar);
        var now = _clock.UtcNow;
        var start = startUtc ?? new DateTime(now.Year, now.Month, now.Day, now.Hour, 0, 0, DateTimeKind.Utc).AddHours(1);
        var localStart = CalendarTime.ToLocal(start, timeZoneId);

        var calendarEvent = new CalendarEvent
        {
            ItemId = UniqueId.GenerateId(),
            CalendarId = calendar.ItemId,
            EventType = descriptor.Name,
            TimeZoneId = timeZoneId,
            AllDay = descriptor.AllDayByDefault,
            Transparency = descriptor.DefaultTransparency,
            Status = descriptor.RequiresApproval ? CalendarEventStatus.Tentative : CalendarEventStatus.Confirmed,
        };

        if (calendarEvent.AllDay)
        {
            calendarEvent.Start = localStart.Date;
            calendarEvent.End = localStart.Date.AddDays(1);
        }
        else
        {
            calendarEvent.Start = localStart;
            calendarEvent.End = localStart.AddMinutes(descriptor.DefaultDurationMinutes);
        }

        CalendarEventExpander.ComputeTimes(calendarEvent);

        return calendarEvent;
    }

    /// <inheritdoc />
    public ValueTask<CalendarEvent> FindByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(id))
        {
            return ValueTask.FromResult<CalendarEvent>(null);
        }

        return _eventStore.FindByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<CalendarEvent>> FindBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
    {
        return _eventStore.ListBySourceAsync(source, sourceId, cancellationToken);
    }

    /// <inheritdoc />
    public Task<CalendarEvent> FindByUidAsync(string calendarId, string uid, CancellationToken cancellationToken = default)
    {
        return _eventStore.FindByUidAsync(calendarId, uid, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> CreateAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        options ??= new CalendarChangeOptions();

        var calendar = await _calendarStore.FindByIdAsync(calendarEvent.CalendarId ?? string.Empty, cancellationToken);

        if (calendar is null)
        {
            return Failed(calendarEvent, nameof(CalendarEvent.CalendarId), S["The calendar doesn't exist."]);
        }

        if (string.IsNullOrEmpty(calendarEvent.ItemId))
        {
            calendarEvent.ItemId = UniqueId.GenerateId();
        }

        var now = _clock.UtcNow;

        calendarEvent.Uid = string.IsNullOrWhiteSpace(calendarEvent.Uid) ? calendarEvent.ItemId : calendarEvent.Uid.Trim();
        calendarEvent.CreatedUtc = now;
        calendarEvent.ModifiedUtc = now;
        calendarEvent.CreatedBy ??= options.Actor;
        calendarEvent.ModifiedBy = options.Actor;

        await PrepareAsync(calendarEvent, calendar);

        var kind = calendarEvent.SeriesId is null ? CalendarEventChangeKind.Created : CalendarEventChangeKind.OccurrenceChanged;

        return await SaveAsync(calendarEvent, null, calendar, kind, options, isNew: true, calendarEvent.OriginalStartUtc);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> UpdateAsync(CalendarEvent calendarEvent, Action<CalendarEvent> change, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(change);

        return await ChangeAsync(calendarEvent, change, null, options);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> UpdateOccurrenceAsync(CalendarEvent series, DateTime originalStartUtc, CalendarEditScope scope, Action<CalendarEvent> change, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);
        ArgumentNullException.ThrowIfNull(change);

        options ??= new CalendarChangeOptions();
        originalStartUtc = DateTime.SpecifyKind(originalStartUtc, DateTimeKind.Utc);

        if (!CalendarEventExpander.TryGetRule(series, out var rule) || series.SeriesId is not null)
        {
            return await UpdateAsync(series, change, options, cancellationToken);
        }

        var existing = (await _eventStore.ListOverridesAsync([series.ItemId], cancellationToken)).FirstOrDefault(item => item.OriginalStartUtc == originalStartUtc);

        if (scope == CalendarEditScope.ThisOccurrence && existing is not null)
        {
            return await UpdateAsync(existing, change, options, cancellationToken);
        }

        if (!CalendarEventExpander.HasOccurrenceAt(series, originalStartUtc) && existing is null)
        {
            return Failed(series, string.Empty, S["The event has no occurrence at that time."]);
        }

        if (scope == CalendarEditScope.AllOccurrences || (scope == CalendarEditScope.ThisAndFollowing && originalStartUtc <= series.StartUtc))
        {
            return await ChangeAsync(series, target => ApplyToSeries(target, originalStartUtc, change), null, options);
        }

        if (scope == CalendarEditScope.ThisOccurrence)
        {
            var occurrence = CreateOverride(series, originalStartUtc);

            change(occurrence);
            occurrence.Recurrence = null;
            occurrence.SeriesId = series.ItemId;
            occurrence.OriginalStartUtc = originalStartUtc;

            return await CreateAsync(occurrence, options, cancellationToken);
        }

        return await SplitAsync(series, rule, originalStartUtc, change, options);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> CancelAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        options ??= new CalendarChangeOptions();

        var result = await ChangeAsync(calendarEvent, target => target.Status = CalendarEventStatus.Cancelled, CalendarEventChangeKind.Cancelled, options);

        if (result.Succeeded && calendarEvent.SeriesId is null && CalendarEventExpander.TryGetRule(calendarEvent, out _))
        {
            foreach (var item in await _eventStore.ListOverridesAsync([calendarEvent.ItemId], cancellationToken))
            {
                if (item.Status != CalendarEventStatus.Cancelled)
                {
                    await ChangeAsync(item, target => target.Status = CalendarEventStatus.Cancelled, CalendarEventChangeKind.Cancelled, new CalendarChangeOptions
                    {
                        Actor = options.Actor,
                        Reason = options.Reason,
                        Notify = false,
                        SendInvitations = false,
                    });
                }
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> CancelOccurrenceAsync(CalendarEvent series, DateTime originalStartUtc, CalendarEditScope scope, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(series);

        options ??= new CalendarChangeOptions();
        originalStartUtc = DateTime.SpecifyKind(originalStartUtc, DateTimeKind.Utc);

        if (!CalendarEventExpander.TryGetRule(series, out var rule) || series.SeriesId is not null || scope == CalendarEditScope.AllOccurrences ||
            (scope == CalendarEditScope.ThisAndFollowing && originalStartUtc <= series.StartUtc))
        {
            return await CancelAsync(series, options, cancellationToken);
        }

        var overrides = await _eventStore.ListOverridesAsync([series.ItemId], cancellationToken);

        if (scope == CalendarEditScope.ThisOccurrence)
        {
            var existing = overrides.FirstOrDefault(item => item.OriginalStartUtc == originalStartUtc);

            if (existing is not null)
            {
                return await CancelAsync(existing, options, cancellationToken);
            }

            var localStart = CalendarTime.ToLocal(originalStartUtc, series.TimeZoneId);

            return await ChangeAsync(
                series,
                target =>
                {
                    target.Recurrence.ExceptionDates.Add(target.AllDay ? localStart.Date : localStart);
                },
                CalendarEventChangeKind.Cancelled,
                options,
                originalStartUtc);
        }

        var result = await ChangeAsync(series, target => EndBefore(target, rule, originalStartUtc), CalendarEventChangeKind.Updated, options);

        if (result.Succeeded)
        {
            foreach (var item in overrides.Where(item => item.OriginalStartUtc >= originalStartUtc))
            {
                await DeleteAsync(item, new CalendarChangeOptions { Actor = options.Actor, Notify = false, SendInvitations = false }, cancellationToken);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public async Task DeleteAsync(CalendarEvent calendarEvent, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        options ??= new CalendarChangeOptions();

        var calendar = await _calendarStore.FindByIdAsync(calendarEvent.CalendarId ?? string.Empty, cancellationToken);

        if (calendar is not null)
        {
            var context = CreateContext(calendarEvent, null, calendar, CalendarEventChangeKind.Deleted, options);

            await _handlers.InvokeAsync((handler, context) => handler.DeletingAsync(context), context, _logger);
        }

        if (calendarEvent.SeriesId is null)
        {
            foreach (var item in await _eventStore.ListOverridesAsync([calendarEvent.ItemId], cancellationToken))
            {
                await _eventStore.DeleteAsync(item, cancellationToken);
            }
        }

        await _eventStore.DeleteAsync(calendarEvent, cancellationToken);

        if (options.Notify && calendar is not null)
        {
            _changes.Enqueue(new CalendarEventChange
            {
                Kind = CalendarEventChangeKind.Deleted,
                Event = calendarEvent,
                Previous = calendarEvent,
                Calendar = calendar,
                Actor = options.Actor,
                Reason = options.Reason,
                SendInvitations = options.SendInvitations,
            });
        }
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> AddAttendeeAsync(CalendarEvent calendarEvent, CalendarAttendee attendee, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);
        ArgumentNullException.ThrowIfNull(attendee);

        var key = attendee.GetKey();

        if (key is null)
        {
            return Failed(calendarEvent, nameof(CalendarEvent.Attendees), S["The attendee needs a user, an email address or a resource."]);
        }

        return await UpdateAsync(
            calendarEvent,
            target =>
            {
                var existing = target.Attendees.FirstOrDefault(item => item.GetKey() == key);

                if (existing is null)
                {
                    target.Attendees.Add(attendee);
                }
                else
                {
                    existing.Name = attendee.Name ?? existing.Name;
                    existing.Email = attendee.Email ?? existing.Email;
                    existing.Role = attendee.Role;
                }
            },
            options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> RemoveAttendeeAsync(CalendarEvent calendarEvent, string attendeeKey, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!calendarEvent.Attendees.Any(item => item.GetKey() == attendeeKey))
        {
            return CalendarResult<CalendarEvent>.Success(calendarEvent);
        }

        return await UpdateAsync(
            calendarEvent,
            target =>
            {
                foreach (var item in target.Attendees.Where(item => item.GetKey() == attendeeKey).ToList())
                {
                    target.Attendees.Remove(item);
                }
            },
            options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<CalendarResult<CalendarEvent>> RespondAsync(CalendarEvent calendarEvent, string attendeeKey, CalendarAttendeeResponse response, CalendarChangeOptions options = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        if (!calendarEvent.Attendees.Any(item => item.GetKey() == attendeeKey))
        {
            return Failed(calendarEvent, nameof(CalendarEvent.Attendees), S["The attendee isn't invited to the event."]);
        }

        var now = _clock.UtcNow;

        return await UpdateAsync(
            calendarEvent,
            target =>
            {
                foreach (var item in target.Attendees.Where(item => item.GetKey() == attendeeKey))
                {
                    item.Response = response;
                    item.RespondedUtc = now;
                }
            },
            options, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CalendarError>> ValidateAsync(CalendarEvent calendarEvent, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(calendarEvent);

        var calendar = await _calendarStore.FindByIdAsync(calendarEvent.CalendarId ?? string.Empty, cancellationToken);

        if (calendar is null)
        {
            return [new CalendarError(nameof(CalendarEvent.CalendarId), S["The calendar doesn't exist."])];
        }

        var copy = CalendarRecordCopier.Clone(calendarEvent);

        if (string.IsNullOrEmpty(copy.ItemId))
        {
            copy.ItemId = UniqueId.GenerateId();
        }

        await PrepareAsync(copy, calendar);

        var options = new CalendarChangeOptions();
        var errors = await ValidateCoreAsync(copy, null, calendar, CalendarEventChangeKind.Created, options);

        if (errors.Count == 0)
        {
            var targets = await GetConflictTargetsAsync(copy, calendar);

            errors.AddRange(await _conflictChecker.CheckAsync(copy, targets));
        }

        return errors;
    }

    private async Task<CalendarResult<CalendarEvent>> ChangeAsync(
        CalendarEvent calendarEvent,
        Action<CalendarEvent> change,
        CalendarEventChangeKind? kind,
        CalendarChangeOptions options,
        DateTime? occurrenceStartUtc = null)
    {
        options ??= new CalendarChangeOptions();

        var calendar = await _calendarStore.FindByIdAsync(calendarEvent.CalendarId ?? string.Empty);

        if (calendar is null)
        {
            return Failed(calendarEvent, nameof(CalendarEvent.CalendarId), S["The calendar doesn't exist."]);
        }

        var previous = CalendarRecordCopier.Clone(calendarEvent);

        change(calendarEvent);

        if (!string.Equals(previous.CalendarId, calendarEvent.CalendarId, StringComparison.Ordinal))
        {
            calendar = await _calendarStore.FindByIdAsync(calendarEvent.CalendarId ?? string.Empty);

            if (calendar is null)
            {
                CalendarRecordCopier.CopyTo(previous, calendarEvent);

                return Failed(calendarEvent, nameof(CalendarEvent.CalendarId), S["The calendar doesn't exist."]);
            }
        }

        await PrepareAsync(calendarEvent, calendar);

        calendarEvent.ModifiedUtc = _clock.UtcNow;
        calendarEvent.ModifiedBy = options.Actor;

        if (MattersToAttendees(previous, calendarEvent))
        {
            calendarEvent.Sequence = previous.Sequence + 1;
        }

        var resolvedKind = kind ?? (calendarEvent.Status == CalendarEventStatus.Cancelled && previous.Status != CalendarEventStatus.Cancelled
            ? CalendarEventChangeKind.Cancelled
            : calendarEvent.SeriesId is not null ? CalendarEventChangeKind.OccurrenceChanged : CalendarEventChangeKind.Updated);

        var result = await SaveAsync(calendarEvent, previous, calendar, resolvedKind, options, isNew: false, occurrenceStartUtc ?? calendarEvent.OriginalStartUtc);

        if (!result.Succeeded)
        {
            CalendarRecordCopier.CopyTo(previous, calendarEvent);
        }

        return result;
    }

    private async Task<CalendarResult<CalendarEvent>> SaveAsync(
        CalendarEvent calendarEvent,
        CalendarEvent previous,
        Calendar calendar,
        CalendarEventChangeKind kind,
        CalendarChangeOptions options,
        bool isNew,
        DateTime? occurrenceStartUtc)
    {
        var errors = await ValidateCoreAsync(calendarEvent, previous, calendar, kind, options);

        if (errors.Count > 0)
        {
            return CalendarResult<CalendarEvent>.Failed(calendarEvent, errors);
        }

        var targets = options.SkipConflictCheck ? [] : await GetConflictTargetsAsync(calendarEvent, calendar);
        var context = CreateContext(calendarEvent, previous, calendar, kind, options);

        if (targets.Count == 0)
        {
            await StageAsync(calendarEvent, context, isNew);
        }
        else
        {
            var lockers = new List<ILocker>();

            try
            {
                foreach (var target in targets.OrderBy(target => target.ItemId, StringComparer.Ordinal))
                {
                    var (locker, locked) = await _distributedLock.TryAcquireLockAsync("CALENDAR_" + target.ItemId, _lockTimeout, _lockExpiration);

                    if (!locked)
                    {
                        return CalendarResult<CalendarEvent>.Failed(calendarEvent, [new CalendarError(string.Empty, S["The calendar '{0}' is busy. Try again in a moment.", target.Name], CalendarError.ConflictCode)]);
                    }

                    lockers.Add(locker);
                }

                var conflicts = await _conflictChecker.CheckAsync(calendarEvent, targets);

                if (conflicts.Count > 0)
                {
                    return CalendarResult<CalendarEvent>.Failed(calendarEvent, conflicts);
                }

                foreach (var target in targets)
                {
                    // Saved with a concurrency check: if another request changed the same calendar meanwhile, this commit fails.
                    target.BookingVersion++;
                    await _calendarStore.UpdateAsync(target);
                }

                await StageAsync(calendarEvent, context, isNew);
            }
            finally
            {
                foreach (var locker in lockers)
                {
                    await locker.DisposeAsync();
                }
            }
        }

        if (options.Notify)
        {
            EnqueueChanges(calendarEvent, previous, calendar, kind, options, occurrenceStartUtc);
        }

        return CalendarResult<CalendarEvent>.Success(calendarEvent);
    }

    private async Task StageAsync(CalendarEvent calendarEvent, CalendarEventContext context, bool isNew)
    {
        await _handlers.InvokeAsync((handler, context) => handler.SavingAsync(context), context, _logger);

        CalendarEventExpander.ComputeTimes(calendarEvent);

        if (isNew)
        {
            await _eventStore.CreateAsync(calendarEvent);
        }
        else
        {
            await _eventStore.UpdateAsync(calendarEvent);
        }

        await _handlers.InvokeAsync((handler, context) => handler.SavedAsync(context), context, _logger);
    }

    private void EnqueueChanges(CalendarEvent calendarEvent, CalendarEvent previous, Calendar calendar, CalendarEventChangeKind kind, CalendarChangeOptions options, DateTime? occurrenceStartUtc)
    {
        var snapshot = CalendarRecordCopier.Clone(calendarEvent);

        _changes.Enqueue(new CalendarEventChange
        {
            Kind = kind,
            Event = snapshot,
            Previous = previous,
            Calendar = calendar,
            OccurrenceStartUtc = occurrenceStartUtc,
            Actor = options.Actor,
            Reason = options.Reason,
            SendInvitations = options.SendInvitations,
        });

        var before = previous?.Attendees.Where(item => item.GetKey() is not null).GroupBy(item => item.GetKey()).ToDictionary(group => group.Key, group => group.First())
            ?? [];

        foreach (var attendee in snapshot.Attendees)
        {
            var key = attendee.GetKey();

            if (key is null)
            {
                continue;
            }

            if (!before.TryGetValue(key, out var earlier))
            {
                if (attendee.Role != CalendarAttendeeRole.Organizer)
                {
                    Enqueue(CalendarEventChangeKind.AttendeeAdded, attendee);
                }
            }
            else if (earlier.Response != attendee.Response)
            {
                Enqueue(CalendarEventChangeKind.ResponseChanged, attendee);
            }
        }

        foreach (var (key, attendee) in before)
        {
            if (!snapshot.Attendees.Any(item => item.GetKey() == key))
            {
                Enqueue(CalendarEventChangeKind.AttendeeRemoved, attendee);
            }
        }

        void Enqueue(CalendarEventChangeKind attendeeKind, CalendarAttendee attendee)
        {
            _changes.Enqueue(new CalendarEventChange
            {
                Kind = attendeeKind,
                Event = snapshot,
                Previous = previous,
                Calendar = calendar,
                Attendee = attendee,
                OccurrenceStartUtc = occurrenceStartUtc,
                Actor = options.Actor,
                Reason = options.Reason,
                SendInvitations = options.SendInvitations,
            });
        }
    }

    private async Task PrepareAsync(CalendarEvent calendarEvent, Calendar calendar)
    {
        calendarEvent.Title = calendarEvent.Title?.Trim();
        calendarEvent.Attendees ??= [];
        calendarEvent.ProviderMappings ??= [];

        if (string.IsNullOrWhiteSpace(calendarEvent.TimeZoneId))
        {
            calendarEvent.TimeZoneId = await GetTimeZoneIdAsync(calendar);
        }
        else
        {
            calendarEvent.TimeZoneId = CalendarTime.NormalizeZoneId(calendarEvent.TimeZoneId) ?? calendarEvent.TimeZoneId.Trim();
        }

        if (string.IsNullOrEmpty(calendarEvent.EventType))
        {
            calendarEvent.EventType = _typeManager.FindEventType(calendar.Type, null)?.Name;
        }

        if (calendarEvent.Recurrence is not null)
        {
            calendarEvent.Recurrence.ExceptionDates ??= [];

            if (string.IsNullOrWhiteSpace(calendarEvent.Recurrence.Rule))
            {
                calendarEvent.Recurrence = null;
            }
            else
            {
                var rule = calendarEvent.Recurrence.Rule.Trim();

                calendarEvent.Recurrence.Rule = rule.StartsWith("RRULE:", StringComparison.OrdinalIgnoreCase) ? rule.Substring(6) : rule;
            }
        }

        var attendees = new List<CalendarAttendee>();

        foreach (var attendee in calendarEvent.Attendees)
        {
            var key = attendee.GetKey();

            if (key is null || attendees.Any(item => item.GetKey() == key))
            {
                continue;
            }

            if (!string.IsNullOrEmpty(attendee.ResourceCalendarId))
            {
                attendee.Role = CalendarAttendeeRole.Resource;
            }

            attendee.Email = attendee.Email?.Trim();
            attendees.Add(attendee);
        }

        calendarEvent.Attendees = attendees;

        CalendarEventExpander.ComputeTimes(calendarEvent);
    }

    private async Task<List<CalendarError>> ValidateCoreAsync(CalendarEvent calendarEvent, CalendarEvent previous, Calendar calendar, CalendarEventChangeKind kind, CalendarChangeOptions options)
    {
        var errors = new List<CalendarError>();
        var calendarType = _typeManager.FindType(calendar.Type);
        var eventType = _typeManager.FindEventType(calendar.Type, calendarEvent.EventType);

        if (string.IsNullOrWhiteSpace(calendarEvent.Title))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.Title), S["The title is required."]));
        }
        else if (calendarEvent.Title.Length > 500)
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.Title), S["The title can't be longer than 500 characters."]));
        }

        if (!calendar.Enabled && calendarEvent.Status != CalendarEventStatus.Cancelled && (previous is null || previous.StartUtc != calendarEvent.StartUtc || previous.EndUtc != calendarEvent.EndUtc))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.CalendarId), S["The calendar '{0}' is disabled and takes no new events.", calendar.Name]));
        }

        if (calendarType is not null && eventType is null)
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.EventType), S["'{0}' is not an event type of {1} calendars.", calendarEvent.EventType ?? string.Empty, calendarType.Name]));
        }

        if (!CalendarTime.IsValidZone(calendarEvent.TimeZoneId))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.TimeZoneId), S["'{0}' is not a time zone.", calendarEvent.TimeZoneId ?? string.Empty]));
        }

        if (!calendarEvent.AllDay && calendarEvent.End <= calendarEvent.Start)
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.End), S["The event must end after it starts."]));
        }

        if (calendarEvent.AllDay && calendarType is not null && !calendarType.Supports(CalendarCapabilities.AllDay))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.AllDay), S["Events on {0} calendars can't last whole days.", calendarType.Name]));
        }

        if (calendarEvent.Recurrence is not null)
        {
            if (calendarEvent.SeriesId is not null)
            {
                errors.Add(new CalendarError("Recurrence", S["A changed occurrence of a repeating event can't repeat itself."]));
            }
            else if (!RecurrenceRule.TryParse(calendarEvent.Recurrence.Rule, out _, out var ruleError))
            {
                errors.Add(new CalendarError("Recurrence", S["The repeat rule is not valid: {0}", ruleError]));
            }
            else if (calendarType is not null && !calendarType.Supports(CalendarCapabilities.Recurrence))
            {
                errors.Add(new CalendarError("Recurrence", S["Events on {0} calendars can't repeat.", calendarType.Name]));
            }
        }

        if (calendarEvent.Attendees.Count > 0 && calendarType is not null && !calendarType.Supports(CalendarCapabilities.Attendees))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.Attendees), S["Events on {0} calendars can't have attendees.", calendarType.Name]));
        }

        if (eventType is not null && eventType.RequiresAttendee && calendarEvent.Status != CalendarEventStatus.Cancelled &&
            !calendarEvent.Attendees.Any(attendee => attendee.Role != CalendarAttendeeRole.Resource))
        {
            errors.Add(new CalendarError(nameof(CalendarEvent.Attendees), S["Add at least one person to this event."]));
        }

        var resourceIds = calendarEvent.Attendees.Where(attendee => !string.IsNullOrEmpty(attendee.ResourceCalendarId)).Select(attendee => attendee.ResourceCalendarId).Distinct().ToList();

        if (resourceIds.Count > 0)
        {
            var resources = await _calendarStore.GetAsync(resourceIds);

            foreach (var missing in resourceIds.Where(id => !resources.Any(resource => resource.ItemId == id && resource.OwnerKind == CalendarOwnerKind.Resource)))
            {
                errors.Add(new CalendarError(nameof(CalendarEvent.Attendees), S["The resource '{0}' doesn't exist.", missing]));
            }
        }

        var context = new CalendarEventValidatingContext(calendarEvent, previous, calendar, calendarType, eventType, kind, options);

        await _handlers.InvokeAsync((handler, context) => handler.ValidatingAsync(context), context, _logger);

        errors.AddRange(context.Errors);

        return errors;
    }

    private async Task<List<Calendar>> GetConflictTargetsAsync(CalendarEvent calendarEvent, Calendar calendar)
    {
        var targets = new List<Calendar>();

        if (calendarEvent.Status == CalendarEventStatus.Cancelled)
        {
            return targets;
        }

        var eventType = _typeManager.FindEventType(calendar.Type, calendarEvent.EventType);

        if (calendar.ConflictPolicy == CalendarConflictPolicy.LimitConcurrent &&
            calendarEvent.Transparency == CalendarTransparency.Busy &&
            (eventType?.CountsTowardCapacity ?? true))
        {
            targets.Add(calendar);
        }

        var resourceIds = calendarEvent.Attendees
            .Where(attendee => !string.IsNullOrEmpty(attendee.ResourceCalendarId) && attendee.Response != CalendarAttendeeResponse.Declined && attendee.ResourceCalendarId != calendar.ItemId)
            .Select(attendee => attendee.ResourceCalendarId)
            .Distinct()
            .ToList();

        if (resourceIds.Count > 0)
        {
            targets.AddRange((await _calendarStore.GetAsync(resourceIds)).Where(resource => resource.ConflictPolicy == CalendarConflictPolicy.LimitConcurrent));
        }

        return targets;
    }

    private CalendarEventContext CreateContext(CalendarEvent calendarEvent, CalendarEvent previous, Calendar calendar, CalendarEventChangeKind kind, CalendarChangeOptions options)
    {
        return new CalendarEventContext(
            calendarEvent,
            previous,
            calendar,
            _typeManager.FindType(calendar.Type),
            _typeManager.FindEventType(calendar.Type, calendarEvent.EventType),
            kind,
            options);
    }

    private static CalendarEvent CreateOverride(CalendarEvent series, DateTime originalStartUtc)
    {
        var occurrence = CalendarRecordCopier.Clone(series);
        var localStart = CalendarTime.ToLocal(originalStartUtc, series.TimeZoneId);
        var length = series.End - series.Start;

        occurrence.ItemId = UniqueId.GenerateId();
        occurrence.Start = series.AllDay ? localStart.Date : localStart;
        occurrence.End = occurrence.Start + length;
        occurrence.Recurrence = null;
        occurrence.ProviderMappings = [];
        occurrence.Uid = series.Uid;
        occurrence.CreatedBy = null;

        return occurrence;
    }

    private static void ApplyToSeries(CalendarEvent series, DateTime originalStartUtc, Action<CalendarEvent> change)
    {
        var occurrence = CreateOverride(series, originalStartUtc);
        var occurrenceStart = occurrence.Start;

        occurrence.Recurrence = CalendarRecordCopier.Clone(series.Recurrence);
        change(occurrence);

        var shift = occurrence.Start - occurrenceStart;
        var length = occurrence.End - occurrence.Start;

        series.Title = occurrence.Title;
        series.Description = occurrence.Description;
        series.Location = occurrence.Location;
        series.EventType = occurrence.EventType;
        series.Status = occurrence.Status;
        series.Transparency = occurrence.Transparency;
        series.Visibility = occurrence.Visibility;
        series.Color = occurrence.Color;
        series.AllDay = occurrence.AllDay;
        series.TimeZoneId = occurrence.TimeZoneId;
        series.Attendees = occurrence.Attendees;
        series.Properties = occurrence.Properties;
        series.Recurrence = occurrence.Recurrence;
        series.Start += shift;
        series.End = series.Start + length;

        if (series.Recurrence is not null && shift != TimeSpan.Zero)
        {
            series.Recurrence.ExceptionDates = series.Recurrence.ExceptionDates.Select(date => date + shift).ToList();
        }
    }

    private async Task<CalendarResult<CalendarEvent>> SplitAsync(CalendarEvent series, RecurrenceRule rule, DateTime originalStartUtc, Action<CalendarEvent> change, CalendarChangeOptions options)
    {
        var following = CreateOverride(series, originalStartUtc);
        var followingRule = RecurrenceRule.Parse(rule.ToString());

        if (rule.Count.HasValue)
        {
            var before = CalendarEventExpander.Expand(series, series.StartUtc, originalStartUtc, maxOccurrences: rule.Count.Value).Count();

            followingRule.Count = Math.Max(1, rule.Count.Value - before);
        }

        var splitLocal = CalendarTime.ToLocal(originalStartUtc, series.TimeZoneId);

        following.Uid = following.ItemId;
        following.Recurrence = new CalendarRecurrence
        {
            Rule = followingRule.ToString(),
            ExceptionDates = series.Recurrence.ExceptionDates.Where(date => date >= (series.AllDay ? splitLocal.Date : splitLocal)).ToList(),
        };

        change(following);
        following.SeriesId = null;
        following.OriginalStartUtc = null;

        var created = await CreateAsync(following, options);

        if (!created.Succeeded)
        {
            return created;
        }

        var overrides = await _eventStore.ListOverridesAsync([series.ItemId]);
        var ended = await ChangeAsync(series, target => EndBefore(target, rule, originalStartUtc), CalendarEventChangeKind.Updated, new CalendarChangeOptions
        {
            Actor = options.Actor,
            Notify = options.Notify,
            SendInvitations = options.SendInvitations,
            SkipConflictCheck = true,
        });

        if (!ended.Succeeded)
        {
            return ended;
        }

        foreach (var item in overrides.Where(item => item.OriginalStartUtc >= originalStartUtc))
        {
            await DeleteAsync(item, new CalendarChangeOptions { Actor = options.Actor, Notify = false, SendInvitations = false });
        }

        return created;
    }

    private static void EndBefore(CalendarEvent series, RecurrenceRule rule, DateTime originalStartUtc)
    {
        var ending = RecurrenceRule.Parse(rule.ToString());

        ending.Count = null;
        ending.Until = DateTime.SpecifyKind(originalStartUtc.AddSeconds(-1), DateTimeKind.Utc);
        ending.UntilIsUtc = true;
        ending.UntilIsDate = false;

        series.Recurrence.Rule = ending.ToString();
        series.Recurrence.ExceptionDates = series.Recurrence.ExceptionDates
            .Where(date => CalendarTime.ToUtc(date, series.TimeZoneId) < originalStartUtc)
            .ToList();
    }

    private static bool MattersToAttendees(CalendarEvent previous, CalendarEvent current)
    {
        return previous.StartUtc != current.StartUtc ||
            previous.EndUtc != current.EndUtc ||
            previous.Status != current.Status ||
            !string.Equals(previous.Title, current.Title, StringComparison.Ordinal) ||
            !string.Equals(previous.Location, current.Location, StringComparison.Ordinal) ||
            !string.Equals(previous.Recurrence?.Rule, current.Recurrence?.Rule, StringComparison.Ordinal);
    }

    private async Task<string> GetTimeZoneIdAsync(Calendar calendar)
    {
        if (!string.IsNullOrWhiteSpace(calendar?.TimeZoneId) && CalendarTime.IsValidZone(calendar.TimeZoneId))
        {
            return CalendarTime.NormalizeZoneId(calendar.TimeZoneId);
        }

        var site = await _siteService.GetSiteSettingsAsync();

        return CalendarTime.NormalizeZoneId(site.TimeZoneId) ?? "UTC";
    }

    private static CalendarResult<CalendarEvent> Failed(CalendarEvent calendarEvent, string field, string message)
    {
        return CalendarResult<CalendarEvent>.Failed(calendarEvent, [new CalendarError(field, message)]);
    }
}
