using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Core.Services;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Scheduling.Core.Services;

/// <summary>
/// Works out bookable start times from the resources' hours and their busy calendar time.
/// </summary>
public sealed class DefaultSchedulingAvailabilityService : ISchedulingAvailabilityService
{
    private readonly SchedulingResourceStore _resourceStore;
    private readonly AppointmentStore _appointmentStore;
    private readonly AppointmentTypeStore _typeStore;
    private readonly ICalendarAvailabilityService _availabilityService;
    private readonly ICalendarQueryService _queryService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSchedulingAvailabilityService"/> class.
    /// </summary>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="appointmentStore">The appointment store.</param>
    /// <param name="typeStore">The appointment type store, for the free time booked appointments keep.</param>
    /// <param name="availabilityService">The calendar free/busy service.</param>
    /// <param name="queryService">The calendar occurrence query service, for shifts.</param>
    /// <param name="clock">The clock.</param>
    public DefaultSchedulingAvailabilityService(
        SchedulingResourceStore resourceStore,
        AppointmentStore appointmentStore,
        AppointmentTypeStore typeStore,
        ICalendarAvailabilityService availabilityService,
        ICalendarQueryService queryService,
        IClock clock)
    {
        _resourceStore = resourceStore;
        _appointmentStore = appointmentStore;
        _typeStore = typeStore;
        _availabilityService = availabilityService;
        _queryService = queryService;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentSlot>> FindSlotsAsync(AppointmentType appointmentType, DateTime fromUtc, DateTime toUtc, string resourceId = null, int maxResults = 200, string excludeAppointmentId = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(appointmentType);

        var now = _clock.UtcNow;
        var duration = TimeSpan.FromMinutes(Math.Max(5, appointmentType.DurationMinutes));
        var step = TimeSpan.FromMinutes(Math.Max(5, appointmentType.SlotIntervalMinutes));
        var before = TimeSpan.FromMinutes(Math.Max(0, appointmentType.BufferBeforeMinutes));
        var after = TimeSpan.FromMinutes(Math.Max(0, appointmentType.BufferAfterMinutes));
        var earliest = Max(fromUtc, now.AddMinutes(Math.Max(0, appointmentType.MinimumNoticeMinutes)));
        var latest = Min(toUtc, now.AddDays(Math.Max(1, appointmentType.HorizonDays)));

        if (!appointmentType.Enabled || latest <= earliest)
        {
            return [];
        }

        var resourceIds = appointmentType.ResourceIds.Where(id => resourceId is null || id == resourceId).ToList();
        var resources = (await _resourceStore.GetAsync(resourceIds, cancellationToken)).Where(resource => resource.Enabled).ToList();

        if (resources.Count == 0 || (appointmentType.Assignment == AppointmentAssignment.AllTogether && resourceId is null && resources.Count < appointmentType.ResourceIds.Count))
        {
            return [];
        }

        // A weekly limit counts the whole week around each time, so the appointments of the weeks on both sides are read too.
        var reach = appointmentType.MaxPerWeek > 0 ? 7 : 1;
        var appointments = await _appointmentStore.ListForResourcesAsync(resources.Select(resource => resource.ItemId), earliest.AddDays(-reach), latest.AddDays(reach));
        var excludeEvents = appointments.Where(appointment => appointment.ItemId == excludeAppointmentId || (appointmentType.Capacity > 1 && appointment.AppointmentTypeId == appointmentType.ItemId))
            .Select(appointment => appointment.CalendarEventId)
            .Where(id => id is not null)
            .Distinct()
            .ToList();

        var keptTime = await GetKeptTimeAsync(appointmentType, appointments, cancellationToken);
        var free = new Dictionary<string, HashSet<DateTime>>(StringComparer.Ordinal);

        foreach (var resource in resources)
        {
            var windows = await GetWindowsAsync(resource, earliest.AddDays(-1), latest.AddDays(1));
            var busy = DefaultCalendarAvailabilityService.Merge(await _availabilityService.GetBusyAsync(new CalendarBusyQuery
            {
                CalendarIds = string.IsNullOrEmpty(resource.CalendarId) ? [] : [resource.CalendarId],
                UserIds = string.IsNullOrEmpty(resource.UserId) ? [] : [resource.UserId],
                FromUtc = earliest - before - duration,
                ToUtc = latest + after + duration,
                ExcludeEventIds = excludeEvents,
            }, cancellationToken));

            var mine = appointments.Where(appointment => appointment.ResourceIds.Contains(resource.ItemId) && appointment.ItemId != excludeAppointmentId).ToList();
            var starts = new HashSet<DateTime>();

            foreach (var window in windows)
            {
                for (var start = window.StartUtc; start + duration <= window.EndUtc; start += step)
                {
                    if (start < earliest || start >= latest)
                    {
                        continue;
                    }

                    var end = start + duration;

                    if (Overlaps(busy, start - before, end + after) || TakesKeptTime(mine, keptTime, appointmentType, start, end))
                    {
                        continue;
                    }

                    if (appointmentType.Capacity > 1)
                    {
                        var sameSession = mine.Count(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc == start);
                        var otherSessions = mine.Any(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc != start && appointment.StartUtc < end + after && appointment.EndUtc > start - before);

                        if (sameSession >= appointmentType.Capacity || otherSessions)
                        {
                            continue;
                        }
                    }

                    if (appointmentType.MaxPerDay > 0)
                    {
                        var day = CalendarTime.ToLocal(start, resource.TimeZoneId).Date;
                        var count = mine.Where(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && CalendarTime.ToLocal(appointment.StartUtc, resource.TimeZoneId).Date == day)
                            .Select(appointment => appointment.StartUtc)
                            .Distinct()
                            .Count();

                        if (count >= appointmentType.MaxPerDay && !mine.Any(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc == start))
                        {
                            continue;
                        }
                    }

                    if (appointmentType.MaxPerWeek > 0)
                    {
                        var week = WeekStart(CalendarTime.ToLocal(start, resource.TimeZoneId).Date);
                        var count = mine.Where(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && WeekStart(CalendarTime.ToLocal(appointment.StartUtc, resource.TimeZoneId).Date) == week)
                            .Select(appointment => appointment.StartUtc)
                            .Distinct()
                            .Count();

                        if (count >= appointmentType.MaxPerWeek && !mine.Any(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc == start))
                        {
                            continue;
                        }
                    }

                    starts.Add(start);
                }
            }

            free[resource.ItemId] = starts;
        }

        var allStarts = free.Values.SelectMany(starts => starts).Distinct().Order();
        var result = new List<AppointmentSlot>();

        foreach (var start in allStarts)
        {
            var available = resources.Where(resource => free[resource.ItemId].Contains(start)).Select(resource => resource.ItemId).ToList();

            if (appointmentType.Assignment == AppointmentAssignment.AllTogether && resourceId is null && available.Count < resources.Count)
            {
                continue;
            }

            var remaining = appointmentType.Capacity > 1
                ? appointmentType.Capacity - appointments.Count(appointment => appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc == start && appointment.ItemId != excludeAppointmentId && appointment.ResourceIds.Intersect(available).Any())
                : 1;

            result.Add(new AppointmentSlot
            {
                StartUtc = start,
                EndUtc = start + duration,
                ResourceIds = available,
                Remaining = Math.Max(1, remaining),
            });

            if (result.Count >= maxResults)
            {
                break;
            }
        }

        return result;
    }

    private async Task<List<CalendarTimeWindow>> GetWindowsAsync(SchedulingResource resource, DateTime fromUtc, DateTime toUtc)
    {
        var windows = new List<CalendarTimeWindow>();

        if (resource.AvailabilityMode == SchedulingAvailabilityMode.Shifts && !string.IsNullOrEmpty(resource.ShiftCalendarId))
        {
            var shifts = await _queryService.GetOccurrencesAsync(new CalendarOccurrenceQuery
            {
                CalendarIds = [resource.ShiftCalendarId],
                FromUtc = fromUtc,
                ToUtc = toUtc,
                EventTypes = ["Shift"],
            });

            foreach (var shift in shifts.Where(shift => string.IsNullOrEmpty(resource.UserId) || shift.Event.Attendees.Any(attendee => attendee.UserId == resource.UserId)))
            {
                windows.Add(new CalendarTimeWindow(shift.StartUtc, shift.EndUtc));
            }

            return windows;
        }

        var zone = resource.TimeZoneId;
        var firstDay = CalendarTime.ToLocal(fromUtc, zone).Date;
        var lastDay = CalendarTime.ToLocal(toUtc, zone).Date;

        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var dayOverride = resource.DateOverrides.FirstOrDefault(item => item.Date.Date == day);

            if (dayOverride is not null)
            {
                if (!dayOverride.Closed && TryParse(dayOverride.Start, out var start) && TryParse(dayOverride.End, out var end) && end > start)
                {
                    windows.Add(new CalendarTimeWindow(CalendarTime.ToUtc(day + start, zone), CalendarTime.ToUtc(day + end, zone)));
                }

                continue;
            }

            foreach (var hours in resource.WorkingHours.Where(item => item.Day == day.DayOfWeek))
            {
                if (TryParse(hours.Start, out var start) && TryParse(hours.End, out var end) && end > start)
                {
                    windows.Add(new CalendarTimeWindow(CalendarTime.ToUtc(day + start, zone), CalendarTime.ToUtc(day + end, zone)));
                }
            }
        }

        return windows;
    }

    private static bool Overlaps(IReadOnlyList<CalendarTimeWindow> busy, DateTime startUtc, DateTime endUtc)
    {
        return busy.Any(block => block.StartUtc < endUtc && block.EndUtc > startUtc);
    }

    // A booked appointment keeps the free time its own type asks for before and after it. Busy time holds only the appointment
    // itself, so without this a later booking could start the moment it ends.
    private async Task<Dictionary<string, CalendarTimeWindow>> GetKeptTimeAsync(AppointmentType appointmentType, IReadOnlyList<Appointment> appointments, CancellationToken cancellationToken)
    {
        var typeIds = appointments.Select(appointment => appointment.AppointmentTypeId)
            .Where(id => !string.IsNullOrEmpty(id) && id != appointmentType.ItemId)
            .Distinct()
            .ToList();

        var types = typeIds.Count == 0
            ? new Dictionary<string, AppointmentType>(StringComparer.Ordinal)
            : (await _typeStore.GetAsync(typeIds, cancellationToken)).ToDictionary(type => type.ItemId, StringComparer.Ordinal);

        if (!string.IsNullOrEmpty(appointmentType.ItemId))
        {
            types[appointmentType.ItemId] = appointmentType;
        }

        var keptTime = new Dictionary<string, CalendarTimeWindow>(StringComparer.Ordinal);

        foreach (var appointment in appointments)
        {
            if (!types.TryGetValue(appointment.AppointmentTypeId ?? string.Empty, out var type) || (type.BufferBeforeMinutes <= 0 && type.BufferAfterMinutes <= 0))
            {
                continue;
            }

            keptTime[appointment.ItemId] = new CalendarTimeWindow(
                appointment.StartUtc.AddMinutes(-Math.Max(0, type.BufferBeforeMinutes)),
                appointment.EndUtc.AddMinutes(Math.Max(0, type.BufferAfterMinutes)));
        }

        return keptTime;
    }

    private static bool TakesKeptTime(List<Appointment> appointments, Dictionary<string, CalendarTimeWindow> keptTime, AppointmentType appointmentType, DateTime startUtc, DateTime endUtc)
    {
        return appointments.Any(appointment =>
            keptTime.TryGetValue(appointment.ItemId, out var window) &&
            window.StartUtc < endUtc &&
            window.EndUtc > startUtc &&
            !(appointmentType.Capacity > 1 && appointment.AppointmentTypeId == appointmentType.ItemId && appointment.StartUtc == startUtc));
    }

    /// <summary>
    /// Reads a time of day written as HH:mm.
    /// </summary>
    /// <param name="value">The text.</param>
    /// <param name="time">The time of day.</param>
    /// <returns><see langword="true"/> when the text is a time of day.</returns>
    public static bool TryParse(string value, out TimeSpan time)
    {
        return TimeSpan.TryParseExact(value, @"hh\:mm", CultureInfo.InvariantCulture, out time);
    }

    // Weeks run Monday to Sunday.
    private static DateTime WeekStart(DateTime day)
    {
        return day.AddDays(-(((int)day.DayOfWeek + 6) % 7));
    }

    private static DateTime Max(DateTime first, DateTime second)
    {
        return first > second ? first : second;
    }

    private static DateTime Min(DateTime first, DateTime second)
    {
        return first < second ? first : second;
    }
}
