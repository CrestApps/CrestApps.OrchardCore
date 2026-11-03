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
using YesSql;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// Availability and booking on a real SQLite store: working hours, breaks, buffers, notice, limits, group sessions,
/// round robin, concurrent bookings, rescheduling, cancelling with a waitlist, and holds.
/// </summary>
public sealed class SchedulingTests
{
    // Monday 2 November 2026; the harness clock says it is 12:00 UTC (7:00 in New York) that day.
    private static readonly DateTime _day = new(2026, 11, 3);

    [Fact]
    public async Task Slots_FollowWorkingHoursBreaksAndBuffers()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, calendar) = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t =>
        {
            t.DurationMinutes = 30;
            t.SlotIntervalMinutes = 30;
            t.BufferAfterMinutes = 15;
        });

        await harness.CreateEventAsync(calendar.ItemId, _day.AddHours(10), _day.AddHours(10).AddMinutes(30));

        // Act
        var slots = await FindAsync(harness, type);

        // Assert (hours 9-12 and 13-17 New York; the 10:00 event and the 15 minute buffer block 9:30 and 10:00)
        Assert.Equal(["9:00", "10:30", "11:00", "11:30", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00", "16:30"], Local(slots));
    }

    [Fact]
    public async Task Slots_KeepABookedAppointmentsFreeTimeAfterClear()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, _) = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t =>
        {
            t.DurationMinutes = 30;
            t.SlotIntervalMinutes = 30;
            t.BufferAfterMinutes = 15;
        });

        var first = await BookAsync(harness, type, Utc(_day.AddHours(10)));

        // Act
        var slots = await FindAsync(harness, type);
        var second = await BookAsync(harness, type, Utc(_day.AddHours(10).AddMinutes(30)), "Jane Doe", "jane@contoso.com");

        // Assert (the 10:00 appointment needs 10:30-10:45 free, so 10:30 can't be offered or booked)
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(second.Succeeded);
        Assert.Equal(["9:00", "11:00", "11:30", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00", "16:30"], Local(slots));
    }

    [Fact]
    public async Task Slots_KeepABookedAppointmentsFreeTimeBeforeClear()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, _) = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t =>
        {
            t.DurationMinutes = 30;
            t.SlotIntervalMinutes = 30;
            t.BufferBeforeMinutes = 15;
        });

        var first = await BookAsync(harness, type, Utc(_day.AddHours(10).AddMinutes(30)));

        // Act
        var slots = await FindAsync(harness, type);

        // Assert (the 10:30 appointment needs 10:15-10:30 free, so 10:00 is gone; 11:00 needs 10:45-11:00 free, so it is gone too)
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.Equal(["9:00", "9:30", "11:30", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00", "16:30"], Local(slots));
    }

    [Fact]
    public async Task GroupSession_KeepsItsFreeTimeAfter_AndStillTakesItsOwnPlaces()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, _) = await AddResourceAsync(harness, "Studio");
        var type = await AddTypeAsync(harness, [resource.ItemId], t =>
        {
            t.DurationMinutes = 30;
            t.SlotIntervalMinutes = 30;
            t.BufferAfterMinutes = 15;
            t.Capacity = 2;
        });

        var first = await BookAsync(harness, type, Utc(_day.AddHours(10)), "Ann", "ann@contoso.com");

        // Act
        var slots = await FindAsync(harness, type);

        // Assert (10:00 keeps its second place; 10:30 falls in the session's free time after)
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.Equal(["9:00", "10:00", "11:00", "11:30", "13:00", "13:30", "14:00", "14:30", "15:00", "15:30", "16:00", "16:30"], Local(slots));
    }

    [Fact]
    public async Task Slots_RespectNoticeHorizonDateOverridesAndDailyLimit()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, _) = await AddResourceAsync(harness, "Dr. Jones", r => r.DateOverrides.Add(new SchedulingDateOverride { Date = _day.AddDays(1), Closed = true }));
        var type = await AddTypeAsync(harness, [resource.ItemId], t =>
        {
            t.DurationMinutes = 60;
            t.SlotIntervalMinutes = 60;
            t.MinimumNoticeMinutes = 60 * 24 * 2;
            t.HorizonDays = 4;
            t.MaxPerDay = 1;
        });

        // Act
        var slots = await harness.RunAsync(services => services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, Utc(_day), Utc(_day.AddDays(10))), commit: false);
        var booked = await BookAsync(harness, type, slots[0].StartUtc);
        var afterBooking = await harness.RunAsync(services => services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, Utc(_day), Utc(_day.AddDays(10))), commit: false);

        // Assert (the notice moves the first day to Wednesday the 4th, which is closed, and the horizon ends before Friday's hours)
        Assert.All(slots, slot => Assert.Equal(new DateTime(2026, 11, 5), ToLocal(slot.StartUtc).Date));
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.Empty(afterBooking);
    }

    [Fact]
    public async Task Booking_PutsATentativeOrConfirmedEventOnTheResourceCalendar_AndTheTimeIsGone()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, calendar) = await AddResourceAsync(harness, "Room A");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.RequiresConfirmation = true);
        var start = Utc(_day.AddHours(9));

        // Act
        var result = await BookAsync(harness, type, start);
        var again = await BookAsync(harness, type, start);
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendar.ItemId], FromUtc = start.AddHours(-1), ToUtc = start.AddHours(2) });

        // Assert
        Assert.True(result.Succeeded, string.Join(" ", result.Errors));
        Assert.Equal(AppointmentStatus.Requested, result.Appointment.Status);
        Assert.False(again.Succeeded);
        Assert.True(again.Conflict);
        var occurrence = Assert.Single(occurrences);
        Assert.Equal(CalendarEventStatus.Tentative, occurrence.Event.Status);
        Assert.Equal(SchedulingConstants.EventSource, occurrence.Event.Source);
        Assert.False(occurrence.Event.Editable);
    }

    [Fact]
    public async Task Booking_RevalidatesTheTime_AndBumpsTheResourceVersion()
    {
        // Arrange (SQLite allows one writer, so a true race is covered by the calendar concurrency test; here the second
        // request checks the time again after the first committed, and the resource version that guards real databases moves)
        await using var harness = await CreateAsync();
        var (resource, _) = await AddResourceAsync(harness, "Dr. Race");
        var type = await AddTypeAsync(harness, [resource.ItemId]);
        var start = Utc(_day.AddHours(14));

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().BookAsync(Request(type, start, "First")));
        var second = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().BookAsync(Request(type, start, "Second")));
        var version = await harness.RunAsync(async services => (await services.GetRequiredService<SchedulingResourceStore>().FindByIdAsync(resource.ItemId)).BookingVersion, commit: false);

        // Assert
        Assert.True(first.Succeeded);
        Assert.False(second.Succeeded);
        Assert.True(second.Conflict);
        Assert.Equal(1, version);
    }

    [Fact]
    public async Task GroupSession_TakesUpToItsCapacity_OnOneEvent()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, calendar) = await AddResourceAsync(harness, "Studio");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.Capacity = 2);
        var start = Utc(_day.AddHours(10));

        // Act
        var one = await BookAsync(harness, type, start, "Ann", "ann@contoso.com");
        var two = await BookAsync(harness, type, start, "Bob", "bob@contoso.com");
        var three = await BookAsync(harness, type, start, "Cid", "cid@contoso.com");
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendar.ItemId], FromUtc = start.AddHours(-1), ToUtc = start.AddHours(2) });

        // Assert
        Assert.True(one.Succeeded, string.Join(" ", one.Errors));
        Assert.True(two.Succeeded, string.Join(" ", two.Errors));
        Assert.False(three.Succeeded);
        var session = Assert.Single(occurrences);
        Assert.Equal(2, session.Event.Attendees.Count);
        Assert.Equal(one.Appointment.CalendarEventId, two.Appointment.CalendarEventId);
    }

    [Fact]
    public async Task RoundRobin_SharesBookingsOut()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (first, _) = await AddResourceAsync(harness, "Agent One");
        var (second, _) = await AddResourceAsync(harness, "Agent Two");
        var type = await AddTypeAsync(harness, [first.ItemId, second.ItemId], t => t.Assignment = AppointmentAssignment.RoundRobin);

        // Act
        var a = await BookAsync(harness, type, Utc(_day.AddHours(9)), "A", "a@contoso.com");
        var b = await BookAsync(harness, type, Utc(_day.AddHours(10)), "B", "b@contoso.com");

        // Assert
        Assert.NotEqual(a.Appointment.ResourceIds[0], b.Appointment.ResourceIds[0]);
    }

    [Fact]
    public async Task Reschedule_MovesTheCalendarEvent_AndCancelOffersTheTimeToTheWaitlist()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, calendar) = await AddResourceAsync(harness, "Dr. Move");
        var type = await AddTypeAsync(harness, [resource.ItemId]);
        var booked = await BookAsync(harness, type, Utc(_day.AddHours(9)));
        var newStart = Utc(_day.AddHours(15));

        await harness.RunAsync(async services => await services.GetRequiredService<WaitlistStore>().CreateAsync(new WaitlistEntry
        {
            AppointmentTypeId = type.ItemId,
            FromUtc = Utc(_day),
            ToUtc = Utc(_day.AddDays(1)),
            CustomerName = "Waiting Wendy",
            CustomerEmail = "wendy@contoso.com",
            CreatedUtc = CalendarTestHarness.Now,
        }));

        // Act
        var (moved, movePending) = await harness.RunAsync(async services =>
        {
            var appointments = services.GetRequiredService<IAppointmentService>();
            var outcome = await appointments.RescheduleAsync(await appointments.FindByIdAsync(booked.Appointment.ItemId), newStart);

            return (outcome, services.GetRequiredService<AppointmentChangeQueue>().Pending.ToList());
        });

        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendar.ItemId], FromUtc = Utc(_day), ToUtc = Utc(_day.AddDays(1)) });

        var pending = await harness.RunAsync(async services =>
        {
            var appointments = services.GetRequiredService<IAppointmentService>();

            await appointments.CancelAsync(await appointments.FindByIdAsync(booked.Appointment.ItemId), "Customer asked");

            return services.GetRequiredService<AppointmentChangeQueue>().Pending.ToList();
        });

        var afterCancel = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendar.ItemId], FromUtc = Utc(_day), ToUtc = Utc(_day.AddDays(1)) });

        // Assert
        Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));
        Assert.Equal(newStart, Assert.Single(occurrences).StartUtc);
        Assert.Equal(booked.Appointment.CalendarEventId, moved.Appointment.CalendarEventId);
        Assert.Empty(afterCancel);
        Assert.Contains(movePending, change => change.Kind == AppointmentChangeKind.Rescheduled);
        Assert.Contains(movePending, change => change.Kind == AppointmentChangeKind.WaitlistSlotAvailable && change.WaitlistEntry.CustomerName == "Waiting Wendy");
        Assert.Contains(pending, change => change.Kind == AppointmentChangeKind.Cancelled);
    }

    [Fact]
    public async Task ExpiredHolds_FreeTheirTime()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var (resource, calendar) = await AddResourceAsync(harness, "Dr. Hold");
        var type = await AddTypeAsync(harness, [resource.ItemId]);
        var start = Utc(_day.AddHours(11));
        var held = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().BookAsync(Request(type, start, "Holder", hold: true)));

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<AppointmentStore>();
            var appointment = await store.FindByIdAsync(held.Appointment.ItemId);

            appointment.HoldExpiresUtc = CalendarTestHarness.Now.AddMinutes(-1);
            await store.UpdateAsync(appointment);
        });

        // Act
        var expired = await harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().ExpireHoldsAsync());
        var slots = await FindAsync(harness, type);
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery { CalendarIds = [calendar.ItemId], FromUtc = start.AddHours(-1), ToUtc = start.AddHours(2) });

        // Assert
        Assert.Equal(1, expired);
        Assert.Contains("11:00", Local(slots));
        Assert.Empty(occurrences);
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        return CalendarTestHarness.CreateAsync(
            services => services.AddSchedulingCore(),
            SchedulingIndexSchema.CreateAsync,
            [new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider()],
            [SchedulingConstants.CollectionName]);
    }

    private static async Task<(SchedulingResource Resource, Calendar Calendar)> AddResourceAsync(CalendarTestHarness harness, string name, Action<SchedulingResource> configure = null)
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

        return (resource, calendar);
    }

    private static async Task<AppointmentType> AddTypeAsync(CalendarTestHarness harness, IList<string> resourceIds, Action<AppointmentType> configure = null)
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

    private static Task<IReadOnlyList<AppointmentSlot>> FindAsync(CalendarTestHarness harness, AppointmentType type)
    {
        return harness.RunAsync(services => services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, Utc(_day), Utc(_day.AddDays(1))), commit: false);
    }

    private static Task<AppointmentResult> BookAsync(CalendarTestHarness harness, AppointmentType type, DateTime startUtc, string name = "John Smith", string email = "john@contoso.com")
    {
        return harness.RunAsync(services => services.GetRequiredService<IAppointmentService>().BookAsync(Request(type, startUtc, name, email)));
    }

    private static AppointmentRequest Request(AppointmentType type, DateTime startUtc, string name, string email = null, bool hold = false)
    {
        return new AppointmentRequest
        {
            AppointmentTypeId = type.ItemId,
            StartUtc = startUtc,
            CustomerName = name,
            CustomerEmail = email ?? name.ToLowerInvariant().Replace(' ', '.') + "@contoso.com",
            Source = "Test",
            Hold = hold,
        };
    }

    private static List<string> Local(IEnumerable<AppointmentSlot> slots)
    {
        return slots.Select(slot => ToLocal(slot.StartUtc).ToString("H:mm", null)).ToList();
    }

    private static DateTime ToLocal(DateTime utc)
    {
        return CrestApps.OrchardCore.Calendars.Core.CalendarTime.ToLocal(utc, "America/New_York");
    }

    private static DateTime Utc(DateTime local)
    {
        return CrestApps.OrchardCore.Calendars.Core.CalendarTime.ToUtc(local, "America/New_York");
    }
}
