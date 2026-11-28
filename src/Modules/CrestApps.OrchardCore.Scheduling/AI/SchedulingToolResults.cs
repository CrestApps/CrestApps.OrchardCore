using CrestApps.OrchardCore.Scheduling.Models;

namespace CrestApps.OrchardCore.Scheduling.AI;

/// <summary>
/// Builds the scheduling AI tools' results from explicit lists of fields. An appointment's manage token is never part of one.
/// </summary>
internal static class SchedulingToolResults
{
    public const string AppointmentNotFound = "There is no appointment with that ID. Use listAppointments to find it.";

    public const string AppointmentTypeNotFound = "There is no appointment type with that ID. Use listAppointmentTypes to find it; it lists enabled types only, so a turned-off type's ID is on the Appointment Types page or in getAppointment's appointmentType.";

    public const string WaitlistEntryNotFound = "There is no waitlist entry with that ID. Use listAppointmentWaitlist to find it.";

    public static object AppointmentSummary(Appointment appointment, IReadOnlyDictionary<string, AppointmentType> types, string siteTimeZoneId)
    {
        var zone = SchedulingToolSupport.ZoneOf(appointment.TimeZoneId, siteTimeZoneId);

        return new
        {
            id = appointment.ItemId,
            appointmentTypeId = appointment.AppointmentTypeId,
            appointmentType = types.TryGetValue(appointment.AppointmentTypeId ?? string.Empty, out var type) ? type.Name : null,
            start = SchedulingToolSupport.Local(appointment.StartUtc, zone),
            end = SchedulingToolSupport.Local(appointment.EndUtc, zone),
            timeZoneId = zone,
            status = appointment.Status.ToString(),
            customerName = appointment.CustomerName,
        };
    }

    public static object AppointmentDetail(Appointment appointment, AppointmentType type, IEnumerable<SchedulingResource> resources, string siteTimeZoneId, string adminPath)
    {
        var zone = SchedulingToolSupport.ZoneOf(appointment.TimeZoneId, siteTimeZoneId);
        var byId = resources.ToDictionary(resource => resource.ItemId, StringComparer.Ordinal);

        return new
        {
            id = appointment.ItemId,
            appointmentType = new { id = appointment.AppointmentTypeId, name = type?.Name },
            start = SchedulingToolSupport.Local(appointment.StartUtc, zone),
            end = SchedulingToolSupport.Local(appointment.EndUtc, zone),
            timeZoneId = zone,
            status = appointment.Status.ToString(),
            holdExpires = SchedulingToolSupport.Local(appointment.HoldExpiresUtc, zone),
            resources = (appointment.ResourceIds ?? []).Select(id => new
            {
                id,
                name = byId.TryGetValue(id, out var resource) ? resource.Name : null,
                kind = byId.TryGetValue(id, out var found) ? found.Kind.ToString() : null,
            }).ToList(),
            customer = new
            {
                name = appointment.CustomerName,
                email = appointment.CustomerEmail,
                phone = appointment.CustomerPhone,
                userId = appointment.UserId,
            },
            notes = appointment.Notes,
            cancelReason = appointment.CancelReason,
            source = appointment.Source,
            calendarEventId = appointment.CalendarEventId,
            created = SchedulingToolSupport.Local(appointment.CreatedUtc, zone),
            modified = SchedulingToolSupport.Local(appointment.ModifiedUtc, zone),
            adminPath,
        };
    }

    public static object AppointmentTypeDetail(AppointmentType type, IReadOnlyDictionary<string, SchedulingResource> resources, string bookingPath, string adminPath)
    {
        return new
        {
            id = type.ItemId,
            name = type.Name,
            description = type.Description,
            slug = type.Slug,
            bookingPath,
            durationMinutes = type.DurationMinutes,
            slotIntervalMinutes = type.SlotIntervalMinutes,
            bufferBeforeMinutes = type.BufferBeforeMinutes,
            bufferAfterMinutes = type.BufferAfterMinutes,
            minimumNoticeMinutes = type.MinimumNoticeMinutes,
            horizonDays = type.HorizonDays,
            maxPerDay = type.MaxPerDay,
            maxPerWeek = type.MaxPerWeek,
            maxUpcomingPerCustomer = type.MaxUpcomingPerCustomer,
            cancelNoticeMinutes = type.CancelNoticeMinutes,
            rescheduleNoticeMinutes = type.RescheduleNoticeMinutes,
            capacity = type.Capacity,
            requiresConfirmation = type.RequiresConfirmation,
            holdMinutes = type.HoldMinutes,
            allowSelfService = type.AllowSelfService,
            location = type.Location,
            color = type.Color,
            enabled = type.Enabled,
            resourceGroups = type.GetResourceRequirements()
                .Where(requirement => requirement.ResourceIds.Count > 0)
                .Select(requirement => new
                {
                    name = requirement.Name,
                    assignment = requirement.Assignment.ToString(),
                    resources = requirement.ResourceIds.Select(id => new
                    {
                        id,
                        name = resources.TryGetValue(id, out var resource) ? resource.Name : null,
                        weight = requirement.GetWeight(id),
                    }).ToList(),
                })
                .ToList(),
            adminPath,
        };
    }

    public static object WaitlistItem(WaitlistEntry entry, IReadOnlyDictionary<string, AppointmentType> types, string siteTimeZoneId)
    {
        return new
        {
            id = entry.ItemId,
            appointmentTypeId = entry.AppointmentTypeId,
            appointmentType = types.TryGetValue(entry.AppointmentTypeId ?? string.Empty, out var type) ? type.Name : null,

            // The page stores whole days from the start of the first to the end of the last, in the site's time zone.
            fromDay = SchedulingToolSupport.LocalDay(entry.FromUtc, siteTimeZoneId),
            toDay = SchedulingToolSupport.LocalDay(entry.ToUtc.AddTicks(-1), siteTimeZoneId),
            customerName = entry.CustomerName,
            customerEmail = entry.CustomerEmail,
            customerPhone = entry.CustomerPhone,
            status = entry.Status.ToString(),
            created = SchedulingToolSupport.Local(entry.CreatedUtc, siteTimeZoneId),
            notified = SchedulingToolSupport.Local(entry.NotifiedUtc, siteTimeZoneId),
        };
    }
}
