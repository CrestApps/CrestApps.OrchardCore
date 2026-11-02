using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using Microsoft.Extensions.DependencyInjection;
using static CrestApps.OrchardCore.Tests.Scheduling.SchedulingTestSupport;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// The appointment type's booking policies: the cancel and reschedule notice and the per-customer limit hold customers who
/// book themselves and let staff through; the weekly limit per resource closes times for everyone.
/// </summary>
public sealed class SchedulingPolicyTests
{
    [Fact]
    public async Task CancelNotice_BlocksTheCustomer_ButNotStaff()
    {
        // Arrange (the appointment starts 26 hours from now; customers must cancel 2 days ahead)
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.CancelNoticeMinutes = 2 * 24 * 60);
        var booked = await BookAsync(harness, Request(type, Day.AddHours(9)));

        // Act
        var byCustomer = await CancelAsync(harness, booked.Appointment.ItemId, selfService: true);
        var byStaff = await CancelAsync(harness, booked.Appointment.ItemId, selfService: false);

        // Assert
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.False(byCustomer.Succeeded);
        Assert.Contains("2 days", Assert.Single(byCustomer.Errors));
        Assert.True(byStaff.Succeeded, string.Join(" ", byStaff.Errors));
        Assert.Equal(AppointmentStatus.Cancelled, byStaff.Appointment.Status);
    }

    [Fact]
    public async Task CancelNotice_LetsTheCustomerCancelEarlyEnough()
    {
        // Arrange (26 hours ahead, with a 24 hour notice)
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.CancelNoticeMinutes = 24 * 60);
        var booked = await BookAsync(harness, Request(type, Day.AddHours(9), selfService: true));

        // Act
        var byCustomer = await CancelAsync(harness, booked.Appointment.ItemId, selfService: true);

        // Assert
        Assert.True(byCustomer.Succeeded, string.Join(" ", byCustomer.Errors));
    }

    [Fact]
    public async Task RescheduleNotice_BlocksTheCustomer_ButNotStaff()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.RescheduleNoticeMinutes = 30 * 60);
        var booked = await BookAsync(harness, Request(type, Day.AddHours(9)));

        // Act
        var byCustomer = await MoveAsync(harness, booked.Appointment.ItemId, Day.AddHours(15), selfService: true);
        var byStaff = await MoveAsync(harness, booked.Appointment.ItemId, Day.AddHours(15), selfService: false);

        // Assert
        Assert.False(byCustomer.Succeeded);
        Assert.Contains("30 hours", Assert.Single(byCustomer.Errors));
        Assert.True(byStaff.Succeeded, string.Join(" ", byStaff.Errors));
        Assert.Equal(Utc(Day.AddHours(15)), byStaff.Appointment.StartUtc);
    }

    [Fact]
    public async Task UpcomingLimitPerCustomer_BlocksTheCustomer_ButNotStaffOrOtherCustomers()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.MaxUpcomingPerCustomer = 1);

        // Act
        var first = await BookAsync(harness, Request(type, Day.AddHours(9), selfService: true));
        var second = await BookAsync(harness, Request(type, Day.AddHours(10), email: "JOHN@contoso.com", selfService: true));
        var byStaff = await BookAsync(harness, Request(type, Day.AddHours(11), selfService: false));
        var someoneElse = await BookAsync(harness, Request(type, Day.AddHours(13), "Jane Doe", "jane@contoso.com", selfService: true));

        // Assert (the customer is matched by email whatever its case)
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.False(second.Succeeded);
        Assert.False(second.Conflict);
        Assert.Contains("1 upcoming", Assert.Single(second.Errors));
        Assert.True(byStaff.Succeeded, string.Join(" ", byStaff.Errors));
        Assert.True(someoneElse.Succeeded, string.Join(" ", someoneElse.Errors));
    }

    [Fact]
    public async Task UpcomingLimitPerCustomer_MatchesByUserId_AndFreesUpWhenOneIsCancelled()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.MaxUpcomingPerCustomer = 1);
        var first = Request(type, Day.AddHours(9), email: "work@contoso.com", selfService: true);
        var second = Request(type, Day.AddHours(10), email: "home@contoso.com", selfService: true);

        first.UserId = "user-1";
        second.UserId = "user-1";

        // Act
        var booked = await BookAsync(harness, first);
        var blocked = await BookAsync(harness, second);

        await CancelAsync(harness, booked.Appointment.ItemId, selfService: true);

        var afterCancel = await BookAsync(harness, second);

        // Assert
        Assert.True(booked.Succeeded, string.Join(" ", booked.Errors));
        Assert.False(blocked.Succeeded);
        Assert.True(afterCancel.Succeeded, string.Join(" ", afterCancel.Errors));
    }

    [Fact]
    public async Task WeeklyLimitPerResource_ClosesTheRestOfTheWeek_ForEveryone()
    {
        // Arrange (weeks run Monday 2 to Sunday 8 November)
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Week");
        var type = await AddTypeAsync(harness, [resource.ItemId], t => t.MaxPerWeek = 2);

        var tuesday = await BookAsync(harness, Request(type, Day.AddHours(9)));
        var wednesday = await BookAsync(harness, Request(type, Day.AddDays(1).AddHours(9), "Jane Doe", "jane@contoso.com"));

        // Act
        var slots = await FindAsync(harness, type, Day, Day.AddDays(7));
        var staffThursday = await BookAsync(harness, Request(type, Day.AddDays(2).AddHours(9), "Ann", "ann@contoso.com"));

        // Assert
        Assert.True(tuesday.Succeeded, string.Join(" ", tuesday.Errors));
        Assert.True(wednesday.Succeeded, string.Join(" ", wednesday.Errors));
        Assert.NotEmpty(slots);
        Assert.All(slots, slot => Assert.True(ToLocal(slot.StartUtc).Date >= new DateTime(2026, 11, 9), $"{ToLocal(slot.StartUtc)} is in the full week."));
        Assert.False(staffThursday.Succeeded);
        Assert.True(staffThursday.Conflict);
    }

    [Fact]
    public async Task Policies_AreOffByDefault()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var resource = await AddResourceAsync(harness, "Dr. Smith");
        var type = await AddTypeAsync(harness, [resource.ItemId]);

        // Act
        var first = await BookAsync(harness, Request(type, Day.AddHours(9), selfService: true));
        var second = await BookAsync(harness, Request(type, Day.AddHours(10), selfService: true));
        var moved = await MoveAsync(harness, first.Appointment.ItemId, Day.AddHours(13), selfService: true);
        var cancelled = await CancelAsync(harness, second.Appointment.ItemId, selfService: true);

        // Assert
        Assert.True(first.Succeeded, string.Join(" ", first.Errors));
        Assert.True(second.Succeeded, string.Join(" ", second.Errors));
        Assert.True(moved.Succeeded, string.Join(" ", moved.Errors));
        Assert.True(cancelled.Succeeded, string.Join(" ", cancelled.Errors));
    }

    private static Task<AppointmentResult> CancelAsync(Calendars.CalendarTestHarness harness, string appointmentId, bool selfService)
    {
        return harness.RunAsync(async services =>
        {
            var appointments = services.GetRequiredService<IAppointmentService>();

            return await appointments.CancelAsync(await appointments.FindByIdAsync(appointmentId), "Test", selfService: selfService);
        });
    }

    private static Task<AppointmentResult> MoveAsync(Calendars.CalendarTestHarness harness, string appointmentId, DateTime startLocal, bool selfService)
    {
        return harness.RunAsync(async services =>
        {
            var appointments = services.GetRequiredService<IAppointmentService>();

            return await appointments.RescheduleAsync(await appointments.FindByIdAsync(appointmentId), Utc(startLocal), selfService: selfService);
        });
    }
}
