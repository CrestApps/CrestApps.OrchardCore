using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Calendars;

/// <summary>
/// Who can do what with a calendar, what a viewer sees, and free/busy time.
/// </summary>
public sealed class CalendarAccessAndAvailabilityTests
{
    private static readonly string _access = CalendarPermissions.AccessCalendars.Name;

    [Fact]
    public async Task AccessLevel_ComesFromOwnershipTeamSharingVisibilityAndPermissions()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var personal = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");
        var team = await harness.CreateCalendarAsync(CalendarTestHarness.Team, "Support", c => c.OwnerId = "Support");
        var shared = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Projects", c =>
        {
            c.Visibility = CalendarVisibility.Private;
            c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane", Access = CalendarAccessLevel.Write });
        });
        var company = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company", c => c.Visibility = CalendarVisibility.Public);

        var john = CalendarTestHarness.User("john", _access);
        var jane = CalendarTestHarness.User("jane", _access, "role:Support");
        var auditor = CalendarTestHarness.User("auditor", CalendarPermissions.ViewAllCalendars.Name);
        var admin = CalendarTestHarness.User("admin", CalendarPermissions.ManageAllCalendars.Name);
        var stranger = CalendarTestHarness.User("stranger");

        // Act
        var levels = await harness.RunAsync(async services =>
        {
            var access = services.GetRequiredService<ICalendarAccessService>();

            return new Dictionary<string, CalendarAccessLevel>
            {
                ["john-personal"] = await access.GetAccessLevelAsync(john, personal),
                ["jane-personal"] = await access.GetAccessLevelAsync(jane, personal),
                ["jane-team"] = await access.GetAccessLevelAsync(jane, team),
                ["john-team"] = await access.GetAccessLevelAsync(john, team),
                ["jane-shared"] = await access.GetAccessLevelAsync(jane, shared),
                ["john-shared"] = await access.GetAccessLevelAsync(john, shared),
                ["john-company"] = await access.GetAccessLevelAsync(john, company),
                ["auditor-shared"] = await access.GetAccessLevelAsync(auditor, shared),
                ["admin-personal"] = await access.GetAccessLevelAsync(admin, personal),
                ["stranger-company"] = await access.GetAccessLevelAsync(stranger, company),
            };
        }, commit: false);

        // Assert
        Assert.Equal(CalendarAccessLevel.Manage, levels["john-personal"]);
        Assert.Equal(CalendarAccessLevel.FreeBusy, levels["jane-personal"]);
        Assert.Equal(CalendarAccessLevel.Write, levels["jane-team"]);
        Assert.Equal(CalendarAccessLevel.None, levels["john-team"]);
        Assert.Equal(CalendarAccessLevel.Write, levels["jane-shared"]);
        Assert.Equal(CalendarAccessLevel.None, levels["john-shared"]);
        Assert.Equal(CalendarAccessLevel.Read, levels["john-company"]);
        Assert.Equal(CalendarAccessLevel.Read, levels["auditor-shared"]);
        Assert.Equal(CalendarAccessLevel.Manage, levels["admin-personal"]);
        Assert.Equal(CalendarAccessLevel.None, levels["stranger-company"]);
    }

    [Fact]
    public async Task ListAccessible_FindsOwnedTeamSharedAndVisibleCalendars()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        await harness.CreateCalendarAsync(CalendarTestHarness.Team, "Support", c => c.OwnerId = "Support");
        await harness.CreateCalendarAsync(CalendarTestHarness.General, "Hidden");
        await harness.CreateCalendarAsync(CalendarTestHarness.General, "Shared with Jane", c => c.Shares.Add(new CalendarShare { PrincipalKind = CalendarPrincipalKind.User, PrincipalId = "jane" }));
        await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");

        var jane = CalendarTestHarness.User("jane", _access, "role:Support");

        // Act
        var readable = await harness.RunAsync(services => services.GetRequiredService<ICalendarAccessService>().ListAccessibleAsync(jane, CalendarAccessLevel.Read), commit: false);
        var any = await harness.RunAsync(services => services.GetRequiredService<ICalendarAccessService>().ListAccessibleAsync(jane, CalendarAccessLevel.FreeBusy), commit: false);

        // Assert
        Assert.Equal(["Shared with Jane", "Support"], readable.Select(access => access.Calendar.Name));
        Assert.Equal(["John", "Shared with Jane", "Support"], any.Select(access => access.Calendar.Name));
    }

    [Fact]
    public async Task Viewer_SeesBusyTimeOnly_OnCalendarsTheyCantRead_UnlessInvited()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var john = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "John", c => c.OwnerId = "john");

        await harness.CreateEventAsync(john.ItemId, Local(9), Local(10), e => e.Title = "Dentist");
        await harness.CreateEventAsync(john.ItemId, Local(11), Local(12), e =>
        {
            e.Title = "1:1 with Jane";
            e.Attendees.Add(new CalendarAttendee { UserId = "jane" });
        });

        // Act
        var occurrences = await harness.GetOccurrencesAsync(new CalendarOccurrenceQuery
        {
            CalendarIds = [john.ItemId],
            FromUtc = CalendarTestHarness.Now.AddDays(-1),
            ToUtc = CalendarTestHarness.Now.AddDays(1),
            Viewer = CalendarTestHarness.User("jane", _access),
        });

        // Assert
        Assert.Equal(2, occurrences.Count);
        Assert.True(occurrences.Single(occurrence => occurrence.Event.Title == "Dentist").BusyOnly);
        Assert.False(occurrences.Single(occurrence => occurrence.Event.Title == "1:1 with Jane").BusyOnly);
    }

    [Fact]
    public async Task UserBusyTime_CombinesTheirCalendarsAndInvitations_AndSkipsDeclinedOnes()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();
        var jane = await harness.CreateCalendarAsync(CalendarTestHarness.Personal, "Jane", c => c.OwnerId = "jane");
        var company = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Company");

        await harness.CreateEventAsync(jane.ItemId, Local(9), Local(10));
        await harness.CreateEventAsync(company.ItemId, Local(13), Local(14), e => e.Attendees.Add(new CalendarAttendee { UserId = "jane", Response = CalendarAttendeeResponse.Accepted }));
        await harness.CreateEventAsync(company.ItemId, Local(15), Local(16), e => e.Attendees.Add(new CalendarAttendee { UserId = "jane", Response = CalendarAttendeeResponse.Declined }));
        await harness.CreateEventAsync(jane.ItemId, Local(16), Local(17), e => e.Transparency = CalendarTransparency.Free);

        var query = new CalendarBusyQuery
        {
            UserIds = ["jane"],
            FromUtc = Utc(Local(8)),
            ToUtc = Utc(Local(18)),
        };

        // Act
        var busy = await harness.RunAsync(services => services.GetRequiredService<ICalendarAvailabilityService>().GetBusyAsync(query), commit: false);
        var free = await harness.RunAsync(services => services.GetRequiredService<ICalendarAvailabilityService>().FindFreeWindowsAsync(query, TimeSpan.FromHours(2)), commit: false);

        // Assert
        Assert.Equal([Utc(Local(9)), Utc(Local(13))], busy.Select(interval => interval.StartUtc));
        Assert.Equal(
            [new CalendarTimeWindow(Utc(Local(10)), Utc(Local(13))), new CalendarTimeWindow(Utc(Local(14)), Utc(Local(18)))],
            free);
    }

    [Fact]
    public async Task PersonalCalendar_IsCreatedOnce()
    {
        // Arrange
        await using var harness = await CalendarTestHarness.CreateAsync();

        // Act
        var first = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().GetOrCreatePersonalCalendarAsync("john", "John Smith"));
        var second = await harness.RunAsync(services => services.GetRequiredService<ICalendarService>().GetOrCreatePersonalCalendarAsync("john", "John Smith"));
        var duplicate = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ICalendarService>();
            var calendar = await manager.NewAsync(CalendarTestHarness.Personal);

            calendar.Name = "Another";
            calendar.OwnerId = "john";

            return await manager.CreateAsync(calendar);
        }, commit: false);

        // Assert
        Assert.Equal(first.ItemId, second.ItemId);
        Assert.Equal("John Smith", first.Name);
        Assert.False(duplicate.Succeeded);
    }

    private static DateTime Local(int hour)
    {
        return new DateTime(2026, 11, 2, hour, 0, 0);
    }

    private static DateTime Utc(DateTime local)
    {
        // New York is five hours behind UTC in November.
        return DateTime.SpecifyKind(local.AddHours(5), DateTimeKind.Utc);
    }
}
