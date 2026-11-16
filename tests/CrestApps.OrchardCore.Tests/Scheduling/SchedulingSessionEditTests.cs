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
/// Editing a dated session keeps its registrations in step: a higher capacity offers the freed places to the people
/// waiting, a capacity below the places taken is refused, and new meeting times move every registration and tell its
/// person.
/// </summary>
public sealed class SchedulingSessionEditTests
{
    [Fact]
    public async Task RaisingTheCapacity_OffersEveryFreedPlaceToThePeopleWaiting()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var session = await SaveNewAsync(harness, capacity: 1);
        await RegisterAsync(harness, session, "ana");
        await RegisterAsync(harness, session, "ben");
        await RegisterAsync(harness, session, "cid");
        await RegisterAsync(harness, session, "dee");

        // Act
        var errors = await EditAsync(harness, session.ItemId, edited => edited.Capacity = 3);
        var registrations = await RegistrationsAsync(harness, session);

        // Assert
        Assert.Empty(errors);
        Assert.Equal(AppointmentStatus.Held, registrations.Single(appointment => appointment.UserId == "ben").Status);
        Assert.Equal(AppointmentStatus.Held, registrations.Single(appointment => appointment.UserId == "cid").Status);
        Assert.DoesNotContain(registrations, appointment => appointment.UserId == "dee");
    }

    [Fact]
    public async Task LoweringTheCapacity_BelowThePlacesTaken_IsRefused()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var session = await SaveNewAsync(harness, capacity: 3);
        await RegisterAsync(harness, session, "ana");
        await RegisterAsync(harness, session, "ben");

        // Act
        var errors = await EditAsync(harness, session.ItemId, edited => edited.Capacity = 1);
        var stored = await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(session.ItemId));

        // Assert
        Assert.NotEmpty(errors);
        Assert.Equal(3, stored.Capacity);
    }

    [Fact]
    public async Task NewMeetingTimes_MoveEveryRegistration_AndTellItsPerson()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var session = await SaveNewAsync(harness, capacity: 3);
        var previousStart = session.StartUtc;
        await RegisterAsync(harness, session, "ana");
        await RegisterAsync(harness, session, "ben");
        var newStart = previousStart.AddDays(1).AddHours(1);

        // Act
        var (errors, changes) = await harness.RunAsync(async services =>
        {
            var found = await services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(session.ItemId);

            found.Meetings = [new SchedulingSessionMeeting { StartUtc = newStart, EndUtc = newStart.AddHours(3) }];

            var saved = await services.GetRequiredService<ISchedulingSessionService>().SaveAsync(found);

            return (saved, services.GetRequiredService<AppointmentChangeQueue>().Pending.ToList());
        });
        var registrations = await RegistrationsAsync(harness, session);

        // Assert
        Assert.Empty(errors);
        Assert.All(registrations, appointment =>
        {
            Assert.Equal(newStart, appointment.StartUtc);
            Assert.Equal(newStart.AddHours(3), appointment.EndUtc);
        });
        var rescheduled = changes.Where(change => change.Kind == AppointmentChangeKind.Rescheduled).ToList();
        Assert.Equal(["ana", "ben"], rescheduled.Select(change => change.Appointment.UserId).Order());
        Assert.All(rescheduled, change => Assert.Equal(previousStart, change.PreviousStartUtc));
    }

    private static Task<CalendarTestHarness> CreateAsync()
    {
        var clock = new CrestApps.OrchardCore.Tests.Learning.Assessments.TestClock { UtcNow = CalendarTestHarness.Now };

        return SchedulingTestSupport.CreateAsync(services =>
        {
            services.RemoveAll<IDistributedLock>();
            services.AddSingleton<IDistributedLock, CrestApps.OrchardCore.Tests.Workforce.TimeOffKit.KeyedLock>();
            services.RemoveAll<IClock>();
            services.AddSingleton<IClock>(clock);
        });
    }

    private static async Task<SchedulingSession> SaveNewAsync(CalendarTestHarness harness, int capacity)
    {
        var resource = await SchedulingTestSupport.AddResourceAsync(harness, "Trainer");
        var type = await SchedulingTestSupport.AddTypeAsync(harness, [resource.ItemId]);
        var start = SchedulingTestSupport.Utc(SchedulingTestSupport.Day.AddDays(7).AddHours(9));
        var session = new SchedulingSession
        {
            AppointmentTypeId = type.ItemId,
            Title = "Forklift class",
            Capacity = capacity,
            ResourceIds = [resource.ItemId],
            WaitlistMode = SchedulingWaitlistMode.AutoPromote,
            Meetings = [new SchedulingSessionMeeting { StartUtc = start, EndUtc = start.AddHours(2) }],
        };

        Assert.Empty(await harness.RunAsync(services => services.GetRequiredService<ISchedulingSessionService>().SaveAsync(session)));

        return session;
    }

    // Edits the session as the editor does: the stored session is read, changed and saved in one scope.
    private static Task<IReadOnlyList<string>> EditAsync(CalendarTestHarness harness, string sessionId, Action<SchedulingSession> edit)
        => harness.RunAsync(async services =>
        {
            var found = await services.GetRequiredService<ISchedulingSessionService>().FindByIdAsync(sessionId);

            edit(found);

            return await services.GetRequiredService<ISchedulingSessionService>().SaveAsync(found);
        });

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
