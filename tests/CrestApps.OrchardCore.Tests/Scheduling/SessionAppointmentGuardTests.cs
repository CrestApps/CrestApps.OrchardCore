using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// Review round 8, finding 6.1: the ordinary appointment actions hand a place in a dated session to the session service,
/// and an ordinary appointment of the same type never touches a session's waitlist.
/// </summary>
public sealed class SessionAppointmentGuardTests
{
    [Fact]
    public async Task CancellingAnOrdinaryAppointmentOfTheSameType_DoesNotTouchASessionWaiter()
    {
        // Arrange: a full session on the trainer, and an ordinary booking of the same type with the helper at the same time.
        var (harness, type, helper, session) = await CreateAsync();
        await using var _ = harness;

        await RegisterAsync(harness, session, "ana");
        var ben = await RegisterAsync(harness, session, "ben");
        var request = SchedulingTestSupport.Request(type, SchedulingTestSupport.Day.AddDays(7).AddHours(10));
        request.ResourceId = helper.ItemId;
        var ordinary = await SchedulingTestSupport.BookAsync(harness, request);

        // Act
        var cancelled = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(ordinary.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().CancelAsync(appointment, "Changed plans", "admin");
        });
        var waitlist = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListWaitlistAsync(session.ItemId));

        // Assert
        Assert.True(ordinary.Succeeded, string.Join(" ", ordinary.Errors));
        Assert.True(ben.Waitlisted);
        Assert.True(cancelled.Succeeded, string.Join(" ", cancelled.Errors));
        Assert.Equal(WaitlistStatus.Waiting, Assert.Single(waitlist).Status);
    }

    [Fact]
    public async Task CancellingARegistrationFromTheAppointments_FreesThePlace_AndOffersIt()
    {
        // Arrange
        var (harness, _, _, session) = await CreateAsync();
        await using var _h = harness;

        var ana = await RegisterAsync(harness, session, "ana");
        await RegisterAsync(harness, session, "ben");

        // Act: the admin Do action, the API, the AI tool and the workflow task all call the appointment service.
        var cancelled = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(ana.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().CancelAsync(appointment, "Sick", "admin");
        });
        var registrations = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(session.ItemId));
        var taken = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().CountTakenAsync(session.ItemId));

        // Assert
        Assert.True(cancelled.Succeeded, string.Join(" ", cancelled.Errors));
        Assert.Equal(AppointmentStatus.Cancelled, registrations.Single(appointment => appointment.UserId == "ana").Status);
        Assert.Equal(AppointmentStatus.Held, registrations.Single(appointment => appointment.UserId == "ben").Status);
        Assert.Equal(1, taken);
    }

    [Fact]
    public async Task MarkingARegistrationTakenPlace_RecordsAttendance_AndMovingItIsRefused()
    {
        // Arrange
        var (harness, _, _, session) = await CreateAsync();
        await using var _h = harness;

        var ana = await RegisterAsync(harness, session, "ana");

        // Act
        var moved = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(ana.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().RescheduleAsync(appointment, appointment.StartUtc.AddDays(1), actor: "admin");
        });
        var completed = await harness.RunAsync(async services =>
        {
            var appointment = await services.GetRequiredService<AppointmentStore>().FindByIdAsync(ana.Appointment.ItemId);

            return await services.GetRequiredService<IAppointmentService>().CompleteAsync(appointment, noShow: false, "admin");
        });
        var registration = Assert.Single(await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(session.ItemId)));

        // Assert
        Assert.False(moved.Succeeded);
        Assert.True(completed.Succeeded, string.Join(" ", completed.Errors));
        Assert.Equal(AppointmentStatus.Completed, registration.Status);
        Assert.Equal(AppointmentAttendance.Attended, registration.Attendance);
        Assert.Equal(session.StartUtc, registration.StartUtc);
    }

    private static async Task<(CalendarTestHarness Harness, AppointmentType Type, SchedulingResource Helper, SchedulingSession Session)> CreateAsync()
    {
        var clock = new CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock { UtcNow = CalendarTestHarness.Now };
        var harness = await SchedulingTestSupport.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(clock);
        });
        var trainer = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var helper = await SchedulingTestSupport.AddResourceAsync(harness, "Helper");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [trainer.ItemId, helper.ItemId], configure: type => type.RequiresConfirmation = false);
        var start = SchedulingTestSupport.Utc(SchedulingTestSupport.Day.AddDays(7).AddHours(9));
        var session = new SchedulingSession
        {
            AppointmentTypeId = type.ItemId,
            Title = "Forklift class",
            Capacity = 1,
            ResourceIds = [trainer.ItemId],
            WaitlistMode = SchedulingWaitlistMode.AutoPromote,
            Meetings = [new SchedulingSessionMeeting { StartUtc = start, EndUtc = start.AddHours(2) }],
        };

        Assert.Empty(await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().SaveAsync(session)));

        return (harness, type, helper, session);
    }

    private static Task<SessionRegistrationResult> RegisterAsync(CalendarTestHarness harness, SchedulingSession session, string userId)
        => harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().RegisterAsync(new SessionRegistrationRequest
        {
            SessionId = session.ItemId,
            UserId = userId,
            CustomerName = userId,
            CustomerEmail = userId + "@example.com",
            Source = "Test",
            SourceId = "enrollment-" + userId,
        }));
}
