using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Keeps dated sessions and their registrations. Registering, cancelling, offering a freed place and passing a lapsed
/// offer all run under one lock per session, on fresh counts, so a session never takes more people than its capacity.
/// A registration is an <see cref="Appointment"/> of the session's type covering all its meetings; the session is one
/// calendar event and each confirmed person is an attendee of it.
/// </summary>
public sealed class DefaultSchedulingSessionService : ISchedulingSessionService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(60);

    private readonly SchedulingSessionStore _sessions;
    private readonly SessionRegistrationStore _registrations;
    private readonly AppointmentStore _appointments;
    private readonly AppointmentTypeStore _types;
    private readonly WaitlistStore _waitlist;
    private readonly SchedulingResourceStore _resources;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;
    private readonly AppointmentChangeQueue _changes;
    private readonly IDistributedLock _distributedLock;
    private readonly ISession _session;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    public DefaultSchedulingSessionService(
        SchedulingSessionStore sessions,
        SessionRegistrationStore registrations,
        AppointmentStore appointments,
        AppointmentTypeStore types,
        WaitlistStore waitlist,
        SchedulingResourceStore resources,
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        AppointmentChangeQueue changes,
        IDistributedLock distributedLock,
        ISession session,
        IClock clock,
        IStringLocalizer<DefaultSchedulingSessionService> stringLocalizer)
    {
        _sessions = sessions;
        _registrations = registrations;
        _appointments = appointments;
        _types = types;
        _waitlist = waitlist;
        _resources = resources;
        _calendarService = calendarService;
        _eventManager = eventManager;
        _changes = changes;
        _distributedLock = distributedLock;
        _session = session;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> SaveAsync(SchedulingSession session, string actor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);

        var errors = new List<string>();
        var type = await _types.FindByIdAsync(session.AppointmentTypeId ?? string.Empty, cancellationToken);

        session.Title = session.Title?.Trim();
        session.Meetings = (session.Meetings ?? []).OrderBy(meeting => meeting.StartUtc).ToList();

        if (type is null)
        {
            errors.Add(S["Choose the appointment type the session follows."]);
        }

        if (string.IsNullOrEmpty(session.Title))
        {
            errors.Add(S["Give the session a name."]);
        }

        if (session.Meetings.Count == 0 || session.Meetings.Any(meeting => meeting.EndUtc <= meeting.StartUtc))
        {
            errors.Add(S["Add at least one meeting, each ending after it starts."]);
        }

        if (session.Capacity < 1)
        {
            errors.Add(S["The capacity must be at least one."]);
        }

        if (session.OfferHours < 1)
        {
            session.OfferHours = 1;
        }

        if (errors.Count > 0)
        {
            return errors;
        }

        if (string.IsNullOrEmpty(session.ItemId) || await _sessions.FindByIdAsync(session.ItemId, cancellationToken) is null)
        {
            session.ItemId ??= UniqueId.GenerateId();
            session.CreatedUtc = _clock.UtcNow;
            await _sessions.CreateAsync(session, cancellationToken);
            await PlaceEventAsync(session, type, actor);
            await _sessions.UpdateAsync(session, cancellationToken);
            await _session.SaveChangesAsync(cancellationToken);

            return errors;
        }

        // A change to a session with registrations runs under the session lock, on the stored session and fresh counts.
        var (locker, locked) = await LockAsync(session.ItemId);

        if (!locked)
        {
            errors.Add(S["The session is busy. Try again in a moment."]);

            return errors;
        }

        await using (locker)
        {
            var stored = await _sessions.FindFreshByIdAsync(session.ItemId, cancellationToken);
            var previousStartUtc = stored.StartUtc;
            var previousMeetings = stored.Meetings.Select(meeting => (meeting.StartUtc, meeting.EndUtc)).ToList();
            var taken = await _registrations.CountTakenAsync(stored.ItemId, cancellationToken);

            if (session.Capacity < taken)
            {
                errors.Add(S["{0} places are taken, so the capacity cannot be lower than that. Cancel registrations first.", taken]);

                return errors;
            }

            CopyEditableFields(session, stored);
            await PlaceEventAsync(stored, type, actor);
            await _sessions.UpdateAsync(stored, cancellationToken);

            // New meeting times move every upcoming registration, and each person is told.
            var moved = new List<Appointment>();

            if (stored.Status == SchedulingSessionStatus.Scheduled && !previousMeetings.SequenceEqual(stored.Meetings.Select(meeting => (meeting.StartUtc, meeting.EndUtc))))
            {
                foreach (var appointment in (await _registrations.ListAsync(stored.ItemId, cancellationToken)).Where(appointment => appointment.Status is AppointmentStatus.Held or AppointmentStatus.Requested or AppointmentStatus.Confirmed))
                {
                    appointment.StartUtc = stored.StartUtc;
                    appointment.EndUtc = stored.EndUtc;
                    appointment.ModifiedUtc = _clock.UtcNow;
                    await _appointments.UpdateAsync(appointment, cancellationToken);
                    moved.Add(appointment);
                }
            }

            await _session.SaveChangesAsync(cancellationToken);

            foreach (var appointment in moved)
            {
                _changes.Enqueue(new AppointmentChange
                {
                    Kind = AppointmentChangeKind.Rescheduled,
                    Appointment = appointment,
                    AppointmentType = type,
                    PreviousStartUtc = previousStartUtc,
                    Actor = actor,
                });
            }

            // A higher capacity offers each freed place to the next person waiting.
            if (stored.Status == SchedulingSessionStatus.Scheduled)
            {
                await OfferFreedPlacesAsync(stored, type, actor, cancellationToken);
            }

            session.CalendarEventId = stored.CalendarEventId;
            session.Status = stored.Status;
        }

        return errors;
    }

    private static void CopyEditableFields(SchedulingSession source, SchedulingSession target)
    {
        target.AppointmentTypeId = source.AppointmentTypeId;
        target.Title = source.Title;
        target.Meetings = source.Meetings;
        target.Capacity = source.Capacity;
        target.Location = source.Location;
        target.MeetingUrl = source.MeetingUrl;
        target.ResourceIds = source.ResourceIds ?? [];
        target.RegistrationOpensUtc = source.RegistrationOpensUtc;
        target.RegistrationClosesUtc = source.RegistrationClosesUtc;
        target.WaitlistMode = source.WaitlistMode;
        target.OfferHours = source.OfferHours;
        target.Source = source.Source;
        target.SourceId = source.SourceId;
    }

    /// <inheritdoc />
    public async Task<SchedulingSession> FindByIdAsync(string sessionId, CancellationToken cancellationToken = default)
        => string.IsNullOrEmpty(sessionId) ? null : await _sessions.FindByIdAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SchedulingSession>> ListBySourceAsync(string source, string sourceId, CancellationToken cancellationToken = default)
        => _sessions.ListBySourceAsync(source, sourceId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<SchedulingSession>> ListAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
        => _sessions.ListStartingAsync(fromUtc, toUtc, cancellationToken);

    /// <inheritdoc />
    public async Task<SchedulingSession> CancelAsync(string sessionId, string reason, string actor = null, CancellationToken cancellationToken = default)
    {
        var (locker, locked) = await LockAsync(sessionId);

        if (!locked)
        {
            return null;
        }

        SchedulingSession session;

        await using (locker)
        {
            session = await _sessions.FindFreshByIdAsync(sessionId, cancellationToken);

            if (session is null || session.Status == SchedulingSessionStatus.Cancelled)
            {
                return session;
            }

            var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);

            foreach (var appointment in (await _registrations.ListAsync(sessionId, cancellationToken)).Where(IsActive))
            {
                appointment.Status = AppointmentStatus.Cancelled;
                appointment.CancelReason = reason;
                appointment.ModifiedUtc = _clock.UtcNow;
                await _appointments.UpdateAsync(appointment, cancellationToken);
                Enqueue(AppointmentChangeKind.Cancelled, appointment, type, actor);
            }

            foreach (var entry in (await _registrations.ListWaitlistAsync(sessionId, cancellationToken)).Where(entry => entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified))
            {
                entry.Status = WaitlistStatus.Removed;
                await _waitlist.UpdateAsync(entry, cancellationToken);
            }

            session.Status = SchedulingSessionStatus.Cancelled;
            await _sessions.UpdateAsync(session, cancellationToken);

            if (await _eventManager.FindByIdAsync(session.CalendarEventId, cancellationToken) is { } calendarEvent)
            {
                await _eventManager.CancelAsync(calendarEvent, Options(actor), cancellationToken);
            }

            await _session.SaveChangesAsync(cancellationToken);
        }

        return session;
    }

    /// <inheritdoc />
    public async Task<SessionRegistrationResult> RegisterAsync(SessionRegistrationRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrEmpty(request.UserId) && string.IsNullOrWhiteSpace(request.CustomerEmail))
        {
            return new SessionRegistrationResult { Error = S["A user or an e-mail address is required."] };
        }

        var (locker, locked) = await LockAsync(request.SessionId);

        if (!locked)
        {
            return new SessionRegistrationResult { Error = S["The session is busy. Try again in a moment."] };
        }

        await using (locker)
        {
            var session = await _sessions.FindFreshByIdAsync(request.SessionId, cancellationToken);
            var now = _clock.UtcNow;

            if (session is null || session.Status == SchedulingSessionStatus.Cancelled)
            {
                return new SessionRegistrationResult { Error = S["The session doesn't exist or was cancelled."] };
            }

            if (!request.IgnoreWindow && ((session.RegistrationOpensUtc is { } opens && now < opens) || now >= (session.RegistrationClosesUtc ?? session.StartUtc)))
            {
                return new SessionRegistrationResult { Error = S["Registration for this session is not open."] };
            }

            var registrations = await _registrations.ListAsync(session.ItemId, cancellationToken);

            // Registering again, such as a retried request, keeps the place the person already has.
            if (registrations.FirstOrDefault(appointment => IsActive(appointment) && IsSamePerson(appointment.UserId, appointment.CustomerEmail, request)) is { } existing)
            {
                return new SessionRegistrationResult { Appointment = existing };
            }

            var waits = await _registrations.ListWaitlistAsync(session.ItemId, cancellationToken);

            if (registrations.Count(IsActive) >= session.Capacity)
            {
                if (waits.FirstOrDefault(entry => entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified && IsSamePerson(entry.UserId, entry.CustomerEmail, request)) is { } waiting)
                {
                    // A notice of a free place that someone else took first: the person has no place, so they wait
                    // again, keeping their turn, and are told of the next free place.
                    if (waiting.Status == WaitlistStatus.Notified)
                    {
                        waiting.Status = WaitlistStatus.Waiting;
                        waiting.NotifiedUtc = null;
                        await _waitlist.UpdateAsync(waiting, cancellationToken);
                        await _session.SaveChangesAsync(cancellationToken);
                    }

                    return new SessionRegistrationResult { WaitlistEntry = waiting };
                }

                if (session.WaitlistMode == SchedulingWaitlistMode.None || !request.JoinWaitlistWhenFull)
                {
                    return new SessionRegistrationResult { Error = S["The session is full."] };
                }

                var entry = new WaitlistEntry
                {
                    ItemId = UniqueId.GenerateId(),
                    AppointmentTypeId = session.AppointmentTypeId,
                    SessionId = session.ItemId,
                    FromUtc = session.StartUtc,
                    ToUtc = session.EndUtc,
                    UserId = request.UserId,
                    CustomerName = request.CustomerName,
                    CustomerEmail = request.CustomerEmail?.Trim(),
                    Source = request.Source,
                    SourceId = request.SourceId,
                    Status = WaitlistStatus.Waiting,
                    CreatedUtc = now,
                };

                await _waitlist.CreateAsync(entry, cancellationToken);
                await _session.SaveChangesAsync(cancellationToken);

                return new SessionRegistrationResult { WaitlistEntry = entry };
            }

            var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);
            var appointment = NewAppointment(session, type, request.UserId, request.CustomerName, request.CustomerEmail, request.Source, request.SourceId, held: false);

            await _appointments.CreateAsync(appointment, cancellationToken);

            // Registering directly books the person's wait, such as the one a notice of a free place came from.
            foreach (var entry in waits.Where(entry => entry.Status is WaitlistStatus.Waiting or WaitlistStatus.Notified && IsSamePerson(entry.UserId, entry.CustomerEmail, request)))
            {
                entry.Status = WaitlistStatus.Booked;
                entry.AppointmentId = appointment.ItemId;
                await _waitlist.UpdateAsync(entry, cancellationToken);
            }

            await AddAttendeeAsync(session, appointment, request.Actor);
            await _session.SaveChangesAsync(cancellationToken);
            Enqueue(appointment.Status == AppointmentStatus.Requested ? AppointmentChangeKind.Requested : AppointmentChangeKind.Confirmed, appointment, type, request.Actor);

            return new SessionRegistrationResult { Appointment = appointment };
        }
    }

    /// <inheritdoc />
    public async Task<SessionRegistrationResult> AcceptOfferAsync(string appointmentId, string actor = null, CancellationToken cancellationToken = default)
    {
        var found = await _appointments.FindByIdAsync(appointmentId ?? string.Empty, cancellationToken);

        if (found is null || string.IsNullOrEmpty(found.SessionId))
        {
            return new SessionRegistrationResult { Error = S["The place was not found."] };
        }

        var (locker, locked) = await LockAsync(found.SessionId);

        if (!locked)
        {
            return new SessionRegistrationResult { Error = S["The session is busy. Try again in a moment."] };
        }

        await using (locker)
        {
            var appointment = await FreshAppointmentAsync(appointmentId, cancellationToken);
            var session = await _sessions.FindFreshByIdAsync(found.SessionId, cancellationToken);

            if (appointment is null || session is null || appointment.Status != AppointmentStatus.Held || appointment.HoldExpiresUtc <= _clock.UtcNow)
            {
                return new SessionRegistrationResult { Error = S["The offered place is no longer held."] };
            }

            var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);

            appointment.Status = type?.RequiresConfirmation == true ? AppointmentStatus.Requested : AppointmentStatus.Confirmed;
            appointment.HoldExpiresUtc = null;
            appointment.ModifiedUtc = _clock.UtcNow;
            await _appointments.UpdateAsync(appointment, cancellationToken);
            await MarkWaitlistBookedAsync(session.ItemId, appointment.ItemId, cancellationToken);
            await AddAttendeeAsync(session, appointment, actor);
            await _session.SaveChangesAsync(cancellationToken);
            Enqueue(AppointmentChangeKind.Confirmed, appointment, type, actor);

            return new SessionRegistrationResult { Appointment = appointment };
        }
    }

    /// <inheritdoc />
    public async Task<SessionRegistrationResult> CancelRegistrationAsync(string appointmentId, string reason, string actor = null, CancellationToken cancellationToken = default)
    {
        var found = await _appointments.FindByIdAsync(appointmentId ?? string.Empty, cancellationToken);

        if (found is null || string.IsNullOrEmpty(found.SessionId))
        {
            return new SessionRegistrationResult { Error = S["The registration was not found."] };
        }

        var (locker, locked) = await LockAsync(found.SessionId);

        if (!locked)
        {
            return new SessionRegistrationResult { Error = S["The session is busy. Try again in a moment."] };
        }

        await using (locker)
        {
            var appointment = await FreshAppointmentAsync(appointmentId, cancellationToken);
            var session = await _sessions.FindFreshByIdAsync(found.SessionId, cancellationToken);

            if (appointment is null || session is null || !IsActive(appointment))
            {
                return new SessionRegistrationResult { Appointment = appointment };
            }

            var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);

            appointment.Status = AppointmentStatus.Cancelled;
            appointment.CancelReason = reason;
            appointment.ModifiedUtc = _clock.UtcNow;
            await _appointments.UpdateAsync(appointment, cancellationToken);

            // A declined offer closes the wait it came from, so the person can join the waitlist again.
            foreach (var entry in (await _registrations.ListWaitlistAsync(session.ItemId, cancellationToken)).Where(entry => entry.AppointmentId == appointment.ItemId && entry.Status == WaitlistStatus.Notified))
            {
                entry.Status = WaitlistStatus.Removed;
                await _waitlist.UpdateAsync(entry, cancellationToken);
            }

            await RemoveAttendeeAsync(session, appointment, actor);
            await _session.SaveChangesAsync(cancellationToken);
            Enqueue(AppointmentChangeKind.Cancelled, appointment, type, actor);

            await OfferFreedPlaceAsync(session, type, actor, cancellationToken);

            return new SessionRegistrationResult { Appointment = appointment };
        }
    }

    /// <inheritdoc />
    public async Task<bool> LeaveWaitlistAsync(string waitlistEntryId, CancellationToken cancellationToken = default)
    {
        var entry = await _waitlist.FindByIdAsync(waitlistEntryId ?? string.Empty, cancellationToken);

        if (entry is null || entry.Status is not (WaitlistStatus.Waiting or WaitlistStatus.Notified))
        {
            return false;
        }

        entry.Status = WaitlistStatus.Removed;
        await _waitlist.UpdateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Appointment>> ListRegistrationsAsync(string sessionId, CancellationToken cancellationToken = default)
        => _registrations.ListAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<Appointment>> ListRegistrationsForUserAsync(string userId, CancellationToken cancellationToken = default)
        => _registrations.ListForUserAsync(userId, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<WaitlistEntry>> ListWaitlistAsync(string sessionId, CancellationToken cancellationToken = default)
        => _registrations.ListWaitlistAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public Task<int> CountTakenAsync(string sessionId, CancellationToken cancellationToken = default)
        => _registrations.CountTakenAsync(sessionId, cancellationToken);

    /// <inheritdoc />
    public async Task<int> RecordAttendanceAsync(string sessionId, IEnumerable<SessionAttendanceMark> marks, string actor = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(marks);

        var session = await _sessions.FindByIdAsync(sessionId ?? string.Empty, cancellationToken);

        if (session is null)
        {
            return 0;
        }

        var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);
        var byId = (await _registrations.ListAsync(sessionId, cancellationToken)).ToDictionary(appointment => appointment.ItemId, StringComparer.Ordinal);
        var changed = 0;

        foreach (var mark in marks)
        {
            if (!byId.TryGetValue(mark.AppointmentId ?? string.Empty, out var appointment)
                || appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.Requested or AppointmentStatus.Completed or AppointmentStatus.NoShow))
            {
                continue;
            }

            var minutes = mark.Attendance switch
            {
                AppointmentAttendance.Absent => 0,
                AppointmentAttendance.Attended => mark.MinutesAttended ?? session.TotalMinutes,
                _ => Math.Clamp(mark.MinutesAttended ?? 0, 0, session.TotalMinutes),
            };
            // Not marked takes a marked registration back to its confirmed place.
            var status = mark.Attendance switch
            {
                AppointmentAttendance.Absent => AppointmentStatus.NoShow,
                AppointmentAttendance.Unmarked => appointment.Status is AppointmentStatus.NoShow or AppointmentStatus.Completed ? AppointmentStatus.Confirmed : appointment.Status,
                _ => AppointmentStatus.Completed,
            };

            // Marking the same again, such as a roster saved twice, changes nothing and tells nobody again.
            if (appointment.Attendance == mark.Attendance && appointment.MinutesAttended == minutes && appointment.Status == status)
            {
                continue;
            }

            appointment.Attendance = mark.Attendance;
            appointment.MinutesAttended = mark.Attendance == AppointmentAttendance.Unmarked ? null : minutes;
            appointment.Status = status;
            appointment.ModifiedUtc = _clock.UtcNow;
            await _appointments.UpdateAsync(appointment, cancellationToken);
            changed++;

            if (mark.Attendance != AppointmentAttendance.Unmarked)
            {
                Enqueue(status == AppointmentStatus.NoShow ? AppointmentChangeKind.NoShow : AppointmentChangeKind.Completed, appointment, type, actor);
            }
        }

        if (changed > 0)
        {
            await _session.SaveChangesAsync(cancellationToken);
        }

        return changed;
    }

    /// <inheritdoc />
    public async Task<int> ExpireOffersAsync(CancellationToken cancellationToken = default)
    {
        var expired = 0;

        foreach (var offer in await _registrations.ListExpiredOffersAsync(_clock.UtcNow, cancellationToken))
        {
            var (locker, locked) = await LockAsync(offer.SessionId);

            if (!locked)
            {
                continue;
            }

            await using (locker)
            {
                var appointment = await FreshAppointmentAsync(offer.ItemId, cancellationToken);
                var session = await _sessions.FindFreshByIdAsync(offer.SessionId, cancellationToken);

                if (appointment is null || session is null || appointment.Status != AppointmentStatus.Held || appointment.HoldExpiresUtc > _clock.UtcNow)
                {
                    continue;
                }

                var type = await _types.FindByIdAsync(session.AppointmentTypeId, cancellationToken);

                appointment.Status = AppointmentStatus.Expired;
                appointment.ModifiedUtc = _clock.UtcNow;
                await _appointments.UpdateAsync(appointment, cancellationToken);

                foreach (var entry in (await _registrations.ListWaitlistAsync(session.ItemId, cancellationToken)).Where(entry => entry.AppointmentId == appointment.ItemId && entry.Status == WaitlistStatus.Notified))
                {
                    entry.Status = WaitlistStatus.OfferExpired;
                    await _waitlist.UpdateAsync(entry, cancellationToken);
                }

                await _session.SaveChangesAsync(cancellationToken);
                Enqueue(AppointmentChangeKind.HoldExpired, appointment, type, SchedulingConstants.EventSource);
                expired++;

                // The place passes to the next person waiting.
                await OfferFreedPlaceAsync(session, type, SchedulingConstants.EventSource, cancellationToken);
            }
        }

        return expired;
    }

    // Offers every free place, one person at a time, under the session lock the caller holds, such as after the
    // capacity was raised.
    private async Task OfferFreedPlacesAsync(SchedulingSession session, AppointmentType type, string actor, CancellationToken cancellationToken)
    {
        var free = session.Capacity - await _registrations.CountTakenAsync(session.ItemId, cancellationToken);

        for (var offered = 0; offered < free; offered++)
        {
            if (!await OfferFreedPlaceAsync(session, type, actor, cancellationToken))
            {
                return;
            }
        }
    }

    // Offers a freed place, under the session lock the caller holds: the next person is held a place for the offer
    // hours, or told a place is free, as the session says. Nothing happens while the session is still full.
    private async Task<bool> OfferFreedPlaceAsync(SchedulingSession session, AppointmentType type, string actor, CancellationToken cancellationToken)
    {
        if (session.WaitlistMode == SchedulingWaitlistMode.None || session.StartUtc <= _clock.UtcNow)
        {
            return false;
        }

        if (await _registrations.CountTakenAsync(session.ItemId, cancellationToken) >= session.Capacity)
        {
            return false;
        }

        var entry = await _registrations.FindFirstWaitingAsync(session.ItemId, cancellationToken);

        if (entry is null)
        {
            return false;
        }

        entry.Status = WaitlistStatus.Notified;
        entry.NotifiedUtc = _clock.UtcNow;

        Appointment held = null;

        if (session.WaitlistMode == SchedulingWaitlistMode.AutoPromote)
        {
            held = NewAppointment(session, type, entry.UserId, entry.CustomerName, entry.CustomerEmail, entry.Source, entry.SourceId, held: true);
            held.HoldExpiresUtc = _clock.UtcNow.AddHours(session.OfferHours);
            entry.AppointmentId = held.ItemId;
            entry.OfferExpiresUtc = held.HoldExpiresUtc;
            await _appointments.CreateAsync(held, cancellationToken);
        }

        await _waitlist.UpdateAsync(entry, cancellationToken);
        await _session.SaveChangesAsync(cancellationToken);

        if (held is not null)
        {
            Enqueue(AppointmentChangeKind.Held, held, type, actor);
        }

        _changes.Enqueue(new AppointmentChange
        {
            Kind = AppointmentChangeKind.WaitlistSlotAvailable,
            Appointment = held,
            AppointmentType = type,
            WaitlistEntry = entry,
            PreviousStartUtc = session.StartUtc,
            Actor = actor,
        });

        return true;
    }

    private Appointment NewAppointment(SchedulingSession session, AppointmentType type, string userId, string name, string email, string source, string sourceId, bool held)
    {
        var now = _clock.UtcNow;

        return new Appointment
        {
            ItemId = UniqueId.GenerateId(),
            AppointmentTypeId = session.AppointmentTypeId,
            SessionId = session.ItemId,
            StartUtc = session.StartUtc,
            EndUtc = session.EndUtc,
            Status = held ? AppointmentStatus.Held : type?.RequiresConfirmation == true ? AppointmentStatus.Requested : AppointmentStatus.Confirmed,
            CustomerName = string.IsNullOrWhiteSpace(name) ? email ?? userId : name.Trim(),
            CustomerEmail = email?.Trim(),
            UserId = userId,
            Source = source,
            SourceId = sourceId,
            CreatedUtc = now,
            ModifiedUtc = now,
        };
    }

    private async Task MarkWaitlistBookedAsync(string sessionId, string appointmentId, CancellationToken cancellationToken)
    {
        foreach (var entry in (await _registrations.ListWaitlistAsync(sessionId, cancellationToken)).Where(entry => entry.AppointmentId == appointmentId))
        {
            entry.Status = WaitlistStatus.Booked;
            await _waitlist.UpdateAsync(entry, cancellationToken);
        }
    }

    // The session is one event on the calendar of its first resource, with each meeting's span covered by it.
    private async Task PlaceEventAsync(SchedulingSession session, AppointmentType type, string actor)
    {
        var primary = session.ResourceIds.Count == 0 ? null : (await _resources.GetAsync([session.ResourceIds[0]])).FirstOrDefault();
        var calendar = primary is null ? null : await _calendarService.FindByIdAsync(primary.CalendarId);

        if (calendar is null)
        {
            return;
        }

        var existing = await _eventManager.FindByIdAsync(session.CalendarEventId);

        if (existing is not null && existing.Status != CalendarEventStatus.Cancelled)
        {
            await _eventManager.UpdateAsync(existing, target =>
            {
                target.Title = session.Title;
                target.Start = CalendarTime.ToLocal(session.StartUtc, target.TimeZoneId);
                target.End = CalendarTime.ToLocal(session.EndUtc, target.TimeZoneId);
                target.Location = session.Location ?? type?.Location;
            }, Options(actor));

            return;
        }

        var calendarEvent = await _eventManager.NewAsync(calendar);

        calendarEvent.Title = session.Title;
        calendarEvent.AllDay = false;
        calendarEvent.Start = CalendarTime.ToLocal(session.StartUtc, calendarEvent.TimeZoneId);
        calendarEvent.End = CalendarTime.ToLocal(session.EndUtc, calendarEvent.TimeZoneId);
        calendarEvent.Location = session.Location ?? type?.Location;
        calendarEvent.Color = type?.Color;
        calendarEvent.Status = CalendarEventStatus.Confirmed;
        calendarEvent.Transparency = CalendarTransparency.Busy;
        calendarEvent.Source = SchedulingConstants.EventSource;
        calendarEvent.SourceId = "class:" + session.ItemId;
        calendarEvent.Editable = false;
        calendarEvent.Description = session.MeetingUrl;

        var created = await _eventManager.CreateAsync(calendarEvent, Options(actor));

        if (created.Succeeded)
        {
            session.CalendarEventId = created.Value.ItemId;
        }
    }

    private async Task AddAttendeeAsync(SchedulingSession session, Appointment appointment, string actor)
    {
        if (appointment.Status == AppointmentStatus.Held || await _eventManager.FindByIdAsync(session.CalendarEventId) is not { } calendarEvent)
        {
            return;
        }

        var attendee = new CalendarAttendee
        {
            Name = appointment.CustomerName,
            Email = string.IsNullOrWhiteSpace(appointment.CustomerEmail) ? null : appointment.CustomerEmail,
            UserId = appointment.UserId,
            Role = CalendarAttendeeRole.Required,
            Response = CalendarAttendeeResponse.Accepted,
        };

        if (attendee.GetKey() is not null)
        {
            await _eventManager.AddAttendeeAsync(calendarEvent, attendee, Options(actor));
        }
    }

    private async Task RemoveAttendeeAsync(SchedulingSession session, Appointment appointment, string actor)
    {
        if (await _eventManager.FindByIdAsync(session.CalendarEventId) is not { } calendarEvent)
        {
            return;
        }

        var key = new CalendarAttendee { Email = appointment.CustomerEmail, UserId = appointment.UserId, Name = appointment.CustomerName }.GetKey();

        if (key is not null)
        {
            await _eventManager.RemoveAttendeeAsync(calendarEvent, key, Options(actor));
        }
    }

    private async Task<Appointment> FreshAppointmentAsync(string appointmentId, CancellationToken cancellationToken)
    {
        var found = await _appointments.FindByIdAsync(appointmentId, cancellationToken);

        if (found is null)
        {
            return null;
        }

        _session.Detach(found, SchedulingConstants.CollectionName);

        return await _appointments.FindByIdAsync(appointmentId, cancellationToken);
    }

    private Task<(global::OrchardCore.Locking.ILocker Locker, bool Locked)> LockAsync(string sessionId)
        => _distributedLock.TryAcquireLockAsync("SCHEDULING_SESSION_" + sessionId, _lockTimeout, _lockExpiration);

    private void Enqueue(AppointmentChangeKind kind, Appointment appointment, AppointmentType type, string actor)
        => _changes.Enqueue(new AppointmentChange { Kind = kind, Appointment = appointment, AppointmentType = type, Actor = actor });

    private static CalendarChangeOptions Options(string actor)
        => new() { Actor = actor ?? SchedulingConstants.EventSource, SkipConflictCheck = true };

    private static bool IsActive(Appointment appointment)
        => appointment.Status is AppointmentStatus.Held or AppointmentStatus.Requested or AppointmentStatus.Confirmed or AppointmentStatus.Completed or AppointmentStatus.NoShow;

    private static bool IsSamePerson(string userId, string email, SessionRegistrationRequest request)
        => (!string.IsNullOrEmpty(request.UserId) && string.Equals(userId, request.UserId, StringComparison.Ordinal))
            || (!string.IsNullOrWhiteSpace(request.CustomerEmail) && string.Equals(email?.Trim(), request.CustomerEmail.Trim(), StringComparison.OrdinalIgnoreCase));
}
