using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using OrchardCore.Locking.Distributed;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Scheduling;

/// <summary>
/// Dated sessions (Learning plan 5.5): a capped session fills, the next person waits, a cancellation offers the place to
/// them automatically, an offer that runs out passes to the next, two registrations at once never exceed the capacity,
/// one registration covers several meetings, and a roster marks attendance with minutes once.
/// </summary>
public sealed class SchedulingSessionTests
{
    [Fact]
    public async Task ACappedSessionFills_ACancellationOffersThePlace_AndALapsedOfferPassesToTheNext()
    {
        // Arrange
        var clock = new CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock { UtcNow = CalendarTestHarness.Now };
        await using var harness = await SchedulingTestSupport.CreateAsync(services => Configure(services, clock));
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var session = await SaveAsync(harness, type, resource, capacity: 2);

        // Act
        var ana = await RegisterAsync(harness, session, "ana");
        var ben = await RegisterAsync(harness, session, "ben");
        var cid = await RegisterAsync(harness, session, "cid");
        var dee = await RegisterAsync(harness, session, "dee");
        var anaAgain = await RegisterAsync(harness, session, "ana");

        await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().CancelRegistrationAsync(ana.Appointment.ItemId, "Sick"));
        var cidOffer = (await RegistrationsAsync(harness, session)).Single(appointment => appointment.UserId == "cid");

        // Cid lets the offer run out: it passes to Dee, who accepts it.
        clock.UtcNow = clock.UtcNow.AddHours(25);
        var expired = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ExpireOffersAsync());
        var deeOffer = (await RegistrationsAsync(harness, session)).Single(appointment => appointment.UserId == "dee");
        var accepted = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().AcceptOfferAsync(deeOffer.ItemId));
        var taken = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().CountTakenAsync(session.ItemId));
        var waitlist = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListWaitlistAsync(session.ItemId));

        // Assert
        Assert.NotNull(ana.Appointment);
        Assert.NotNull(ben.Appointment);
        Assert.True(cid.Waitlisted);
        Assert.True(dee.Waitlisted);
        Assert.Equal(ana.Appointment.ItemId, anaAgain.Appointment.ItemId);
        Assert.Equal(AppointmentStatus.Held, cidOffer.Status);
        Assert.Equal("enrollment-cid", cidOffer.SourceId);
        Assert.Equal(1, expired);
        Assert.True(accepted.Succeeded, accepted.Error);
        Assert.Equal(AppointmentStatus.Confirmed, accepted.Appointment.Status);
        Assert.Equal(2, taken);
        Assert.Equal(WaitlistStatus.OfferExpired, waitlist.Single(entry => entry.UserId == "cid").Status);
        Assert.Equal(WaitlistStatus.Booked, waitlist.Single(entry => entry.UserId == "dee").Status);
    }

    [Fact]
    public async Task TwoRegistrationsAtOnce_ForTheLastPlace_TakeOne()
    {
        // Arrange
        await using var harness = await SchedulingTestSupport.CreateAsync(services => Configure(services, new CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock { UtcNow = CalendarTestHarness.Now }));
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var session = await SaveAsync(harness, type, resource, capacity: 1, waitlist: SchedulingWaitlistMode.None);

        // Act
        var results = await Task.WhenAll(RegisterAsync(harness, session, "ana"), RegisterAsync(harness, session, "ben"));

        // Assert
        Assert.Single(results, result => result.Appointment is not null);
        Assert.Single(results, result => result.Error is not null);
    }

    [Fact]
    public async Task OneRegistration_CoversEveryMeeting_AndARosterMarksAttendanceOnce()
    {
        // Arrange: two meetings of two hours.
        await using var harness = await SchedulingTestSupport.CreateAsync(services => Configure(services, new CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock { UtcNow = CalendarTestHarness.Now }));
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var session = await SaveAsync(harness, type, resource, capacity: 5, meetings: 2);
        var ana = await RegisterAsync(harness, session, "ana");
        var ben = await RegisterAsync(harness, session, "ben");
        var cid = await RegisterAsync(harness, session, "cid");

        // Act
        var marks = new[]
        {
            new SessionAttendanceMark(ana.Appointment.ItemId, AppointmentAttendance.Attended),
            new SessionAttendanceMark(ben.Appointment.ItemId, AppointmentAttendance.Partial, 90),
            new SessionAttendanceMark(cid.Appointment.ItemId, AppointmentAttendance.Absent),
        };
        var first = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().RecordAttendanceAsync(session.ItemId, marks));
        var again = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().RecordAttendanceAsync(session.ItemId, marks));
        var registrations = (await RegistrationsAsync(harness, session)).ToDictionary(appointment => appointment.UserId);

        // Assert
        Assert.Equal(session.Meetings[0].StartUtc, ana.Appointment.StartUtc);
        Assert.Equal(session.Meetings[1].EndUtc, ana.Appointment.EndUtc);
        Assert.Equal(3, first);
        Assert.Equal(0, again);
        Assert.Equal(AppointmentStatus.Completed, registrations["ana"].Status);
        Assert.Equal(240, registrations["ana"].MinutesAttended);
        Assert.Equal(90, registrations["ben"].MinutesAttended);
        Assert.Equal(AppointmentAttendance.Partial, registrations["ben"].Attendance);
        Assert.Equal(AppointmentStatus.NoShow, registrations["cid"].Status);
    }

    private static void Configure(IServiceCollection services, CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock clock)
    {
        services.RemoveAll<IDistributedLock>();
        services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
        services.RemoveAll<IClock>();
        services.AddSingleton<IClock>(clock);
    }

    private static async Task<SchedulingSession> SaveAsync(CalendarTestHarness harness, AppointmentType type, SchedulingResource resource, int capacity, SchedulingWaitlistMode waitlist = SchedulingWaitlistMode.AutoPromote, int meetings = 1)
    {
        var start = SchedulingTestSupport.Utc(SchedulingTestSupport.Day.AddDays(7).AddHours(9));
        var session = new SchedulingSession
        {
            AppointmentTypeId = type.ItemId,
            Title = "Forklift class",
            Capacity = capacity,
            ResourceIds = [resource.ItemId],
            WaitlistMode = waitlist,
            Meetings = Enumerable.Range(0, meetings).Select(day => new SchedulingSessionMeeting { StartUtc = start.AddDays(day), EndUtc = start.AddDays(day).AddHours(2) }).ToList(),
        };

        var errors = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().SaveAsync(session));

        Assert.Empty(errors);

        return session;
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

    private static Task<IReadOnlyList<Appointment>> RegistrationsAsync(CalendarTestHarness harness, SchedulingSession session)
        => harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().ListRegistrationsAsync(session.ItemId));
}
