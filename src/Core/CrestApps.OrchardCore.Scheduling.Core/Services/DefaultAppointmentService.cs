using System.Globalization;
using System.Security.Cryptography;
using CrestApps.Core;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.Localization;
using OrchardCore.Locking;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;
using Calendar = CrestApps.OrchardCore.Calendars.Models.Calendar;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Books appointments on the shared calendar. Each booking checks the time again under a lock on every resource it takes, and
/// bumps those resources' versions under a concurrency check, so two requests can never book the same time.
/// </summary>
public sealed class DefaultAppointmentService : IAppointmentService
{
    private static readonly TimeSpan _lockTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan _lockExpiration = TimeSpan.FromSeconds(30);

    private readonly AppointmentTypeStore _typeStore;
    private readonly SchedulingResourceStore _resourceStore;
    private readonly AppointmentStore _appointmentStore;
    private readonly WaitlistStore _waitlistStore;
    private readonly ISchedulingAvailabilityService _availabilityService;
    private readonly ICalendarService _calendarService;
    private readonly ICalendarEventManager _eventManager;
    private readonly AppointmentChangeQueue _changes;
    private readonly IDistributedLock _distributedLock;
    private readonly IClock _clock;

    internal readonly IStringLocalizer S;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultAppointmentService"/> class.
    /// </summary>
    /// <param name="typeStore">The appointment type store.</param>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="appointmentStore">The appointment store.</param>
    /// <param name="waitlistStore">The waitlist store.</param>
    /// <param name="availabilityService">The availability service.</param>
    /// <param name="calendarService">The calendar service.</param>
    /// <param name="eventManager">The calendar event manager.</param>
    /// <param name="changes">The queue of changes for the after-commit observers.</param>
    /// <param name="distributedLock">The distributed lock.</param>
    /// <param name="clock">The clock.</param>
    /// <param name="stringLocalizer">The string localizer.</param>
    public DefaultAppointmentService(
        AppointmentTypeStore typeStore,
        SchedulingResourceStore resourceStore,
        AppointmentStore appointmentStore,
        WaitlistStore waitlistStore,
        ISchedulingAvailabilityService availabilityService,
        ICalendarService calendarService,
        ICalendarEventManager eventManager,
        AppointmentChangeQueue changes,
        IDistributedLock distributedLock,
        IClock clock,
        IStringLocalizer<DefaultAppointmentService> stringLocalizer)
    {
        _typeStore = typeStore;
        _resourceStore = resourceStore;
        _appointmentStore = appointmentStore;
        _waitlistStore = waitlistStore;
        _availabilityService = availabilityService;
        _calendarService = calendarService;
        _eventManager = eventManager;
        _changes = changes;
        _distributedLock = distributedLock;
        _clock = clock;
        S = stringLocalizer;
    }

    /// <inheritdoc />
    public async Task<Appointment> FindByIdAsync(string id)
    {
        return string.IsNullOrEmpty(id) ? null : await _appointmentStore.FindByIdAsync(id);
    }

    /// <inheritdoc />
    public async Task<AppointmentResult> BookAsync(AppointmentRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var result = new AppointmentResult();
        var type = await _typeStore.FindByIdAsync(request.AppointmentTypeId ?? string.Empty);

        if (type is null || !type.Enabled)
        {
            result.Errors.Add(S["The appointment type doesn't exist or can't be booked."]);

            return result;
        }

        if (string.IsNullOrWhiteSpace(request.CustomerName))
        {
            result.Errors.Add(S["The customer's name is required."]);
        }

        if (string.IsNullOrWhiteSpace(request.CustomerEmail) && string.IsNullOrWhiteSpace(request.CustomerPhone) && string.IsNullOrWhiteSpace(request.UserId))
        {
            result.Errors.Add(S["An email address or a phone number is required."]);
        }

        if (!result.Succeeded)
        {
            return result;
        }

        var start = Truncate(request.StartUtc);
        var candidates = type.ResourceIds.Where(id => request.ResourceId is null || id == request.ResourceId).ToList();
        var lockers = await LockAsync(candidates);

        if (lockers is null)
        {
            result.Errors.Add(S["The schedule is busy. Try again in a moment."]);
            result.Conflict = true;

            return result;
        }

        try
        {
            var slot = (await _availabilityService.FindSlotsAsync(type, start, start.AddMinutes(1), request.ResourceId, 1)).FirstOrDefault(item => item.StartUtc == start);

            if (slot is null)
            {
                result.Errors.Add(S["That time is no longer available."]);
                result.Conflict = true;

                return result;
            }

            var resourceIds = await ChooseResourcesAsync(type, slot);
            var now = _clock.UtcNow;
            var appointment = new Appointment
            {
                ItemId = UniqueId.GenerateId(),
                AppointmentTypeId = type.ItemId,
                ResourceIds = resourceIds,
                StartUtc = start,
                EndUtc = start.AddMinutes(type.DurationMinutes),
                TimeZoneId = request.TimeZoneId,
                Status = request.Hold ? AppointmentStatus.Held : type.RequiresConfirmation ? AppointmentStatus.Requested : AppointmentStatus.Confirmed,
                HoldExpiresUtc = request.Hold ? now.AddMinutes(Math.Max(1, type.HoldMinutes)) : null,
                CustomerName = request.CustomerName.Trim(),
                CustomerEmail = request.CustomerEmail?.Trim(),
                CustomerPhone = request.CustomerPhone?.Trim(),
                UserId = request.UserId,
                Notes = request.Notes,
                ManageToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant(),
                Source = request.Source,
                CreatedUtc = now,
                ModifiedUtc = now,
            };

            var eventResult = await PlaceEventAsync(type, appointment, request.Actor);

            if (eventResult is not null)
            {
                result.Errors.Add(eventResult);

                return result;
            }

            await BumpAsync(resourceIds);
            await _appointmentStore.CreateAsync(appointment);

            result.Appointment = appointment;
            Enqueue(appointment.Status switch
            {
                AppointmentStatus.Held => AppointmentChangeKind.Held,
                AppointmentStatus.Requested => AppointmentChangeKind.Requested,
                _ => AppointmentChangeKind.Confirmed,
            }, appointment, type, request.Actor);

            return result;
        }
        finally
        {
            await ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc />
    public async Task<AppointmentResult> ConfirmAsync(Appointment appointment, string actor = null)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var result = new AppointmentResult { Appointment = appointment };

        if (appointment.Status is not (AppointmentStatus.Held or AppointmentStatus.Requested))
        {
            result.Errors.Add(S["Only a held or requested appointment can be confirmed."]);

            return result;
        }

        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);

        appointment.Status = AppointmentStatus.Confirmed;
        appointment.HoldExpiresUtc = null;
        appointment.ModifiedUtc = _clock.UtcNow;

        await SetEventStatusAsync(appointment, type, CalendarEventStatus.Confirmed, actor);
        await _appointmentStore.UpdateAsync(appointment);
        Enqueue(AppointmentChangeKind.Confirmed, appointment, type, actor);

        return result;
    }

    /// <inheritdoc />
    public async Task<AppointmentResult> RescheduleAsync(Appointment appointment, DateTime startUtc, string resourceId = null, string actor = null)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var result = new AppointmentResult { Appointment = appointment };
        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);

        if (type is null || appointment.Status is not (AppointmentStatus.Held or AppointmentStatus.Requested or AppointmentStatus.Confirmed))
        {
            result.Errors.Add(S["Only an upcoming appointment can be moved."]);

            return result;
        }

        var start = Truncate(startUtc);
        var candidates = type.ResourceIds.Where(id => resourceId is null || id == resourceId).ToList();
        var lockers = await LockAsync(candidates);

        if (lockers is null)
        {
            result.Errors.Add(S["The schedule is busy. Try again in a moment."]);
            result.Conflict = true;

            return result;
        }

        try
        {
            var slot = (await _availabilityService.FindSlotsAsync(type, start, start.AddMinutes(1), resourceId, 1, appointment.ItemId)).FirstOrDefault(item => item.StartUtc == start);

            if (slot is null)
            {
                result.Errors.Add(S["That time is no longer available."]);
                result.Conflict = true;

                return result;
            }

            var previousStart = appointment.StartUtc;
            var keep = appointment.ResourceIds.All(slot.ResourceIds.Contains) && type.Assignment != AppointmentAssignment.AllTogether;

            await RemoveEventAsync(appointment, type, actor);

            appointment.ResourceIds = keep ? appointment.ResourceIds : await ChooseResourcesAsync(type, slot);
            appointment.StartUtc = start;
            appointment.EndUtc = start.AddMinutes(type.DurationMinutes);
            appointment.ModifiedUtc = _clock.UtcNow;

            var eventError = await PlaceEventAsync(type, appointment, actor);

            if (eventError is not null)
            {
                result.Errors.Add(eventError);

                return result;
            }

            await BumpAsync(appointment.ResourceIds);
            await _appointmentStore.UpdateAsync(appointment);

            _changes.Enqueue(new AppointmentChange
            {
                Kind = AppointmentChangeKind.Rescheduled,
                Appointment = appointment,
                AppointmentType = type,
                PreviousStartUtc = previousStart,
                Actor = actor,
            });

            await OfferToWaitlistAsync(type, previousStart, actor);

            return result;
        }
        finally
        {
            await ReleaseAsync(lockers);
        }
    }

    /// <inheritdoc />
    public async Task<AppointmentResult> CancelAsync(Appointment appointment, string reason = null, string actor = null)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var result = new AppointmentResult { Appointment = appointment };

        if (appointment.Status is AppointmentStatus.Cancelled or AppointmentStatus.Expired or AppointmentStatus.Completed or AppointmentStatus.NoShow)
        {
            result.Errors.Add(S["The appointment is already finished."]);

            return result;
        }

        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);

        await RemoveEventAsync(appointment, type, actor);

        appointment.Status = AppointmentStatus.Cancelled;
        appointment.CancelReason = reason;
        appointment.ModifiedUtc = _clock.UtcNow;

        await _appointmentStore.UpdateAsync(appointment);
        Enqueue(AppointmentChangeKind.Cancelled, appointment, type, actor);

        if (type is not null)
        {
            await OfferToWaitlistAsync(type, appointment.StartUtc, actor);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task<AppointmentResult> CompleteAsync(Appointment appointment, bool noShow, string actor = null)
    {
        ArgumentNullException.ThrowIfNull(appointment);

        var result = new AppointmentResult { Appointment = appointment };

        if (appointment.Status is not (AppointmentStatus.Confirmed or AppointmentStatus.Requested or AppointmentStatus.Completed or AppointmentStatus.NoShow))
        {
            result.Errors.Add(S["Only a booked appointment can be marked as taken place or missed."]);

            return result;
        }

        var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);

        appointment.Status = noShow ? AppointmentStatus.NoShow : AppointmentStatus.Completed;
        appointment.ModifiedUtc = _clock.UtcNow;

        await _appointmentStore.UpdateAsync(appointment);
        Enqueue(noShow ? AppointmentChangeKind.NoShow : AppointmentChangeKind.Completed, appointment, type, actor);

        return result;
    }

    /// <inheritdoc />
    public async Task<int> ExpireHoldsAsync()
    {
        var expired = await _appointmentStore.ListExpiredHoldsAsync(_clock.UtcNow);

        foreach (var appointment in expired)
        {
            var type = await _typeStore.FindByIdAsync(appointment.AppointmentTypeId);

            await RemoveEventAsync(appointment, type, SchedulingConstants.EventSource);

            appointment.Status = AppointmentStatus.Expired;
            appointment.ModifiedUtc = _clock.UtcNow;

            await _appointmentStore.UpdateAsync(appointment);
            Enqueue(AppointmentChangeKind.HoldExpired, appointment, type, SchedulingConstants.EventSource);
        }

        return expired.Count;
    }

    private async Task<string> PlaceEventAsync(AppointmentType type, Appointment appointment, string actor)
    {
        var resources = (await _resourceStore.GetAsync(appointment.ResourceIds)).ToList();
        var primary = resources.FirstOrDefault(resource => resource.ItemId == appointment.ResourceIds[0]) ?? resources.FirstOrDefault();
        var calendar = primary is null ? null : await _calendarService.FindByIdAsync(primary.CalendarId);

        if (calendar is null)
        {
            return S["The resource has no calendar to put the appointment on."];
        }

        var tentative = appointment.Status is AppointmentStatus.Held or AppointmentStatus.Requested;
        var options = new CalendarChangeOptions { Actor = actor ?? SchedulingConstants.EventSource, SkipConflictCheck = true };
        var customer = new CalendarAttendee
        {
            Name = appointment.CustomerName,
            Email = string.IsNullOrWhiteSpace(appointment.CustomerEmail) ? null : appointment.CustomerEmail,
            UserId = appointment.UserId,
            Role = CalendarAttendeeRole.Required,
            Response = CalendarAttendeeResponse.Accepted,
        };

        if (type.Capacity > 1)
        {
            var sessionKey = string.Create(CultureInfo.InvariantCulture, $"session:{type.ItemId}:{primary.ItemId}:{appointment.StartUtc:yyyyMMddTHHmm}");
            var session = (await _eventManager.FindBySourceAsync(SchedulingConstants.EventSource, sessionKey)).FirstOrDefault(item => item.Status != CalendarEventStatus.Cancelled);

            if (session is not null)
            {
                appointment.CalendarEventId = session.ItemId;

                if (customer.GetKey() is null)
                {
                    return null;
                }

                var added = await _eventManager.AddAttendeeAsync(session, customer, options);

                return added.Succeeded ? null : string.Join(" ", added.Errors.Select(error => error.Message));
            }

            var created = await CreateEventAsync(type, appointment, calendar, resources, primary, sessionKey, tentative, type.Name, customer, options);

            return created;
        }

        return await CreateEventAsync(type, appointment, calendar, resources, primary, appointment.ItemId, tentative, $"{type.Name}: {appointment.CustomerName}", customer, options);
    }

    private async Task<string> CreateEventAsync(AppointmentType type, Appointment appointment, Calendar calendar, IList<SchedulingResource> resources, SchedulingResource primary, string sourceId, bool tentative, string title, CalendarAttendee customer, CalendarChangeOptions options)
    {
        var calendarEvent = await _eventManager.NewAsync(calendar);

        calendarEvent.Title = title;
        calendarEvent.AllDay = false;
        calendarEvent.Start = CalendarTime.ToLocal(appointment.StartUtc, calendarEvent.TimeZoneId);
        calendarEvent.End = CalendarTime.ToLocal(appointment.EndUtc, calendarEvent.TimeZoneId);
        calendarEvent.Location = type.Location;
        calendarEvent.Color = type.Color;
        calendarEvent.Status = tentative ? CalendarEventStatus.Tentative : CalendarEventStatus.Confirmed;
        calendarEvent.Transparency = CalendarTransparency.Busy;
        calendarEvent.Source = SchedulingConstants.EventSource;
        calendarEvent.SourceId = sourceId;
        calendarEvent.Editable = false;
        calendarEvent.Description = appointment.Notes;

        if (customer.GetKey() is not null)
        {
            calendarEvent.Attendees.Add(customer);
        }

        foreach (var resource in resources.Where(resource => resource.ItemId != primary.ItemId))
        {
            var resourceCalendar = await _calendarService.FindByIdAsync(resource.CalendarId);

            if (resourceCalendar?.OwnerKind == CalendarOwnerKind.Resource)
            {
                calendarEvent.Attendees.Add(new CalendarAttendee { ResourceCalendarId = resourceCalendar.ItemId, Name = resource.Name, Role = CalendarAttendeeRole.Resource, Response = CalendarAttendeeResponse.Accepted });
            }
            else if (!string.IsNullOrEmpty(resource.UserId))
            {
                calendarEvent.Attendees.Add(new CalendarAttendee { UserId = resource.UserId, Name = resource.Name, Role = CalendarAttendeeRole.Required, Response = CalendarAttendeeResponse.Accepted });
            }
        }

        var created = await _eventManager.CreateAsync(calendarEvent, options);

        if (!created.Succeeded)
        {
            return string.Join(" ", created.Errors.Select(error => error.Message));
        }

        appointment.CalendarEventId = created.Value.ItemId;

        return null;
    }

    private async Task RemoveEventAsync(Appointment appointment, AppointmentType type, string actor)
    {
        var calendarEvent = await _eventManager.FindByIdAsync(appointment.CalendarEventId);

        if (calendarEvent is null)
        {
            return;
        }

        var options = new CalendarChangeOptions { Actor = actor ?? SchedulingConstants.EventSource, SkipConflictCheck = true };

        if (type?.Capacity > 1 && calendarEvent.SourceId?.StartsWith("session:", StringComparison.Ordinal) == true)
        {
            var key = new CalendarAttendee { Email = appointment.CustomerEmail, UserId = appointment.UserId, Name = appointment.CustomerName }.GetKey();

            if (key is not null)
            {
                await _eventManager.RemoveAttendeeAsync(calendarEvent, key, options);
            }

            if (calendarEvent.Attendees.Any(attendee => attendee.Role != CalendarAttendeeRole.Resource))
            {
                return;
            }
        }

        await _eventManager.CancelAsync(calendarEvent, options);
    }

    private async Task SetEventStatusAsync(Appointment appointment, AppointmentType type, CalendarEventStatus status, string actor)
    {
        var calendarEvent = await _eventManager.FindByIdAsync(appointment.CalendarEventId);

        if (calendarEvent is not null && calendarEvent.Status != status && (type?.Capacity ?? 1) <= 1)
        {
            await _eventManager.UpdateAsync(calendarEvent, target => target.Status = status, new CalendarChangeOptions { Actor = actor ?? SchedulingConstants.EventSource, SkipConflictCheck = true });
        }
    }

    private async Task<List<string>> ChooseResourcesAsync(AppointmentType type, AppointmentSlot slot)
    {
        if (type.Assignment == AppointmentAssignment.AllTogether)
        {
            return slot.ResourceIds.ToList();
        }

        if (type.Assignment == AppointmentAssignment.RoundRobin && slot.ResourceIds.Count > 1)
        {
            var since = _clock.UtcNow.AddDays(-30);
            var counts = new List<(string Id, int Count)>();

            foreach (var id in slot.ResourceIds)
            {
                counts.Add((id, await _appointmentStore.CountForResourceAsync(id, since)));
            }

            return [counts.OrderBy(item => item.Count).ThenBy(item => type.ResourceIds.IndexOf(item.Id)).First().Id];
        }

        return [type.ResourceIds.First(slot.ResourceIds.Contains)];
    }

    private async Task OfferToWaitlistAsync(AppointmentType type, DateTime freedStartUtc, string actor)
    {
        if (freedStartUtc <= _clock.UtcNow)
        {
            return;
        }

        var entry = await _waitlistStore.FindFirstWaitingAsync(type.ItemId, freedStartUtc);

        if (entry is null)
        {
            return;
        }

        entry.Status = WaitlistStatus.Notified;
        entry.NotifiedUtc = _clock.UtcNow;

        await _waitlistStore.UpdateAsync(entry);

        _changes.Enqueue(new AppointmentChange
        {
            Kind = AppointmentChangeKind.WaitlistSlotAvailable,
            AppointmentType = type,
            WaitlistEntry = entry,
            PreviousStartUtc = freedStartUtc,
            Actor = actor,
        });
    }

    private async Task BumpAsync(IEnumerable<string> resourceIds)
    {
        foreach (var resource in await _resourceStore.GetAsync(resourceIds))
        {
            // Saved with a concurrency check: if another booking bumped the same resource meanwhile, this commit fails.
            resource.BookingVersion++;
            await _resourceStore.UpdateAsync(resource);
        }
    }

    private async Task<List<ILocker>> LockAsync(IEnumerable<string> resourceIds)
    {
        var lockers = new List<ILocker>();

        foreach (var id in resourceIds.Distinct().Order(StringComparer.Ordinal))
        {
            var (locker, locked) = await _distributedLock.TryAcquireLockAsync("SCHEDULING_RESOURCE_" + id, _lockTimeout, _lockExpiration);

            if (!locked)
            {
                await ReleaseAsync(lockers);

                return null;
            }

            lockers.Add(locker);
        }

        return lockers;
    }

    private static async Task ReleaseAsync(IEnumerable<ILocker> lockers)
    {
        foreach (var locker in lockers ?? [])
        {
            await locker.DisposeAsync();
        }
    }

    private void Enqueue(AppointmentChangeKind kind, Appointment appointment, AppointmentType type, string actor)
    {
        _changes.Enqueue(new AppointmentChange
        {
            Kind = kind,
            Appointment = appointment,
            AppointmentType = type,
            Actor = actor,
        });
    }

    private static DateTime Truncate(DateTime value)
    {
        var utc = DateTime.SpecifyKind(value, DateTimeKind.Utc);

        return new DateTime(utc.Year, utc.Month, utc.Day, utc.Hour, utc.Minute, 0, DateTimeKind.Utc);
    }
}
