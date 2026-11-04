using System.Globalization;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// Shared setup for the scheduling tests on a real SQLite store. The harness clock says it is Monday 2 November 2026, 12:00 UTC
/// (7:00 in New York), and resources work 9-12 and 13-17 New York time on weekdays.
/// </summary>
internal static class SchedulingTestSupport
{
    /// <summary>
    /// Tuesday 3 November 2026, local to New York.
    /// </summary>
    public static readonly DateTime Day = new(2026, 11, 3);

    public static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSchedulingCore();
                configure?.Invoke(services);
            },
            SchedulingIndexSchema.CreateAsync,
            [new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider()],
            [SchedulingConstants.CollectionName]);
    }

    public static async Task<SchedulingResource> AddResourceAsync(CalendarTestHarness harness, string name, Action<SchedulingResource> configure = null)
    {
        var calendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, name + " calendar");
        var resource = new SchedulingResource
        {
            Name = name,
            CalendarId = calendar.ItemId,
            TimeZoneId = "America/New_York",
        };

        foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday })
        {
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "09:00", End = "12:00" });
            resource.WorkingHours.Add(new SchedulingWorkingHours { Day = day, Start = "13:00", End = "17:00" });
        }

        configure?.Invoke(resource);
        await harness.RunAsync(services => services.GetRequiredService<SchedulingResourceStore>().CreateAsync(resource).AsTask());

        return resource;
    }

    public static async Task<AppointmentType> AddTypeAsync(CalendarTestHarness harness, IList<string> resourceIds, Action<AppointmentType> configure = null)
    {
        var type = new AppointmentType
        {
            Name = "Consultation",
            DurationMinutes = 60,
            SlotIntervalMinutes = 60,
            MinimumNoticeMinutes = 0,
            HorizonDays = 30,
            ResourceIds = resourceIds,
        };

        configure?.Invoke(type);
        await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().CreateAsync(type).AsTask());

        return type;
    }

    public static Task<IReadOnlyList<AppointmentSlot>> FindAsync(CalendarTestHarness harness, AppointmentType type, DateTime fromLocal, DateTime toLocal, string resourceId = null)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, Utc(fromLocal), Utc(toLocal), resourceId), commit: false);
    }

    public static Task<AppointmentResult> BookAsync(CalendarTestHarness harness, AppointmentRequest request)
    {
        return harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().BookAsync(request));
    }

    public static AppointmentRequest Request(AppointmentType type, DateTime startLocal, string name = "John Smith", string email = "john@contoso.com", bool selfService = false)
    {
        return new AppointmentRequest
        {
            AppointmentTypeId = type.ItemId,
            StartUtc = Utc(startLocal),
            CustomerName = name,
            CustomerEmail = email,
            Source = "Test",
            SelfService = selfService,
        };
    }

    public static List<string> Local(IEnumerable<AppointmentSlot> slots)
    {
        return slots.Select(slot => ToLocal(slot.StartUtc).ToString("ddd H:mm", CultureInfo.InvariantCulture)).ToList();
    }

    public static DateTime ToLocal(DateTime utc)
    {
        return CalendarTime.ToLocal(utc, "America/New_York");
    }

    public static DateTime Utc(DateTime local)
    {
        return CalendarTime.ToUtc(local, "America/New_York");
    }

    public static Task<IReadOnlyList<CalendarOccurrence>> OccurrencesAsync(CalendarTestHarness harness, string calendarId)
    {
        return harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendarId], FromUtc = Utc(Day.AddDays(-1)), ToUtc = Utc(Day.AddDays(14)) });
    }
}
