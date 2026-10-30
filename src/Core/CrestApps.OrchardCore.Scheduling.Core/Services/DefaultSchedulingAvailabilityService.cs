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
    private readonly ICalendarAvailabilityService _availabilityService;
    private readonly ICalendarQueryService _queryService;
    private readonly IClock _clock;

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultSchedulingAvailabilityService"/> class.
    /// </summary>
    /// <param name="resourceStore">The resource store.</param>
    /// <param name="appointmentStore">The appointment store.</param>
    /// <param name="availabilityService">The calendar free/busy service.</param>
    /// <param name="queryService">The calendar occurrence query service, for shifts.</param>
    /// <param name="clock">The clock.</param>
    public DefaultSchedulingAvailabilityService(
        SchedulingResourceStore resourceStore,
        AppointmentStore appointmentStore,
        ICalendarAvailabilityService availabilityService,
        ICalendarQueryService queryService,
        IClock clock)
    {
        _resourceStore = resourceStore;
        _appointmentStore = appointmentStore;
        _availabilityService = availabilityService;
        _queryService = queryService;
        _clock = clock;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AppointmentSlot>> FindSlotsAsync(AppointmentType appointmentType, DateTime fromUtc, DateTime toUtc, string resourceId = null, int maxResults = 200, string excludeAppointmentId = null)
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
        var resources = (await _resourceStore.GetAsync(resourceIds)).Where(resource => resource.Enabled).ToList();

        if (resources.Count == 0 || (appointmentType.Assignment == AppointmentAssignment.AllTogether && resourceId is null && resources.Count < appointmentType.ResourceIds.Count))
        {
            return [];
        }

        var appointments = await _appointmentStore.ListForResourcesAsync(resources.Select(resource => resource.ItemId), earliest.AddDays(-1), latest.AddDays(1));
        var excludeEvents = appointments.Where(appointment => appointment.ItemId == excludeAppointmentId || (appointmentType.Capacity > 1 && appointment.AppointmentTypeId == appointmentType.ItemId))
            .Select(appointment => appointment.CalendarEventId)
            .Where(id => id is not null)
            .Distinct()
            .ToList();

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
            }));

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

                    if (Overlaps(busy, start - before, end + after))
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

    private static DateTime Max(DateTime first, DateTime second)
    {
        return first > second ? first : second;
    }

    private static DateTime Min(DateTime first, DateTime second)
    {
        return first < second ? first : second;
    }
}
