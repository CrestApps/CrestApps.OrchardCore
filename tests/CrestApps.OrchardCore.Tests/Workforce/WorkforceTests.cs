using CrestApps.Core;
using CrestApps.OrchardCore.Calendars;
using CrestApps.OrchardCore.Calendars.Core;
using CrestApps.OrchardCore.Calendars.Models;
using CrestApps.OrchardCore.Calendars.Services;
using CrestApps.OrchardCore.Scheduling;
using CrestApps.OrchardCore.Scheduling.Core;
using CrestApps.OrchardCore.Scheduling.Core.Indexes;
using CrestApps.OrchardCore.Scheduling.Core.Services;
using CrestApps.OrchardCore.Scheduling.Models;
using CrestApps.OrchardCore.Scheduling.Services;
using CrestApps.OrchardCore.Skills;
using CrestApps.OrchardCore.Skills.Core;
using CrestApps.OrchardCore.Skills.Core.Indexes;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Workforce;
using CrestApps.OrchardCore.Workforce.Core;
using CrestApps.OrchardCore.Workforce.Core.Indexes;
using CrestApps.OrchardCore.Workforce.Core.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using YesSql.Indexes;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The workforce base on a real SQLite store: the team tree and its calendars, workers, manager scoping, calendar
/// access by team, activity codes, and breaks inside a published shift keeping Scheduling from booking them.
/// </summary>
public sealed class WorkforceTests
{
    // Tuesday 3 November 2026 in New York; the harness clock says it is Monday 2 November.
    private static readonly DateTime _day = new(2026, 11, 3);

    [Fact]
    public async Task Teams_FormATree_AndASubtreeHoldsEveryLevelBelow()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var region = await AddTeamAsync(harness, "East", TeamKind.Region);
        var site = await AddTeamAsync(harness, "Boston", TeamKind.Site, region.ItemId);
        var team = await AddTeamAsync(harness, "Billing", TeamKind.Team, site.ItemId);
        var other = await AddTeamAsync(harness, "West", TeamKind.Region);

        // Act
        var tree = await harness.RunAsync(services => services.GetRequiredService<ITeamManager>().ListTreeAsync(), commit: false);
        var subtree = await harness.RunAsync(services => services.GetRequiredService<ITeamManager>().GetSubtreeIdsAsync(region.ItemId), commit: false);
        var ancestors = await harness.RunAsync(services => services.GetRequiredService<ITeamManager>().GetAncestorsAsync(team.ItemId), commit: false);

        // Assert
        Assert.Equal(["East:0", "Boston:1", "Billing:2", "West:0"], tree.Select(item => $"{item.Team.Name}:{item.Depth}"));
        Assert.Equal(new[] { region.ItemId, site.ItemId, team.ItemId }.Order(StringComparer.Ordinal), subtree.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(other.ItemId, subtree);
        Assert.Equal(["Boston", "East"], ancestors.Select(ancestor => ancestor.Name));
    }

    [Fact]
    public async Task ATeam_CannotGoUnderItself_OrUnderATeamThatDoesNotExist()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var region = await AddTeamAsync(harness, "East", TeamKind.Region);
        var site = await AddTeamAsync(harness, "Boston", TeamKind.Site, region.ItemId);

        // Act
        var (underChild, underMissing, unnamed) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITeamManager>();
            var root = await manager.FindByIdAsync(region.ItemId);

            root.ParentId = site.ItemId;
            var cycle = await manager.ValidateAsync(root);

            root.ParentId = "missing";
            var missing = await manager.ValidateAsync(root);

            var blank = await manager.NewAsync();

            return (cycle, missing, await manager.ValidateAsync(blank));
        }, commit: false);

        // Assert
        Assert.False(underChild.Succeeded);
        Assert.False(underMissing.Succeeded);
        Assert.False(unnamed.Succeeded);
    }

    [Fact]
    public async Task ATeam_GetsAWorkforceCalendar_ThatFollowsItsName()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var team = await AddTeamAsync(harness, "Billing", TeamKind.Team);

        // Act
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITeamManager>();
            var saved = await manager.FindByIdAsync(team.ItemId);

            saved.Name = "Billing and Payments";
            await manager.UpdateAsync(saved);
        });

        var (calendar, savedTeam) = await harness.RunAsync(async services =>
        {
            var found = await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, team.ItemId);
            var stored = await services.GetRequiredService<ITeamManager>().FindByIdAsync(team.ItemId);

            return (found, stored);
        }, commit: false);

        // Assert
        Assert.NotNull(calendar);
        Assert.Equal(WorkforceConstants.CalendarType, calendar.Type);
        Assert.Equal("Billing and Payments", calendar.Name);
        Assert.Equal(calendar.ItemId, savedTeam.CalendarId);
    }

    [Fact]
    public async Task Workers_NeedAName_AndTheirEmployeeNumberAndUserAreTheirsAlone()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await AddWorkerAsync(harness, "Ana Lopez", worker =>
        {
            worker.EmployeeNumber = "E100";
            worker.UserId = "ana";
        });

        // Act
        var (sameNumber, sameUser, unnamed) = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IWorkerManager>();
            var first = await manager.NewAsync();

            first.DisplayName = "Ben";
            first.EmployeeNumber = "e100";

            var second = await manager.NewAsync();

            second.DisplayName = "Cara";
            second.UserId = "ana";

            return (await manager.ValidateAsync(first), await manager.ValidateAsync(second), await manager.ValidateAsync(await manager.NewAsync()));
        }, commit: false);

        var found = await harness.RunAsync(services => services.GetRequiredService<IWorkerManager>().FindByUserIdAsync("ana"), commit: false);

        // Assert
        Assert.False(sameNumber.Succeeded);
        Assert.False(sameUser.Succeeded);
        Assert.False(unnamed.Succeeded);
        Assert.Equal("Ana Lopez", found?.DisplayName);
    }

    [Fact]
    public async Task Search_ByATeam_IncludesItsSubTeams_AndListByTeam_HonorsTheMembershipDates()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var site = await AddTeamAsync(harness, "Boston", TeamKind.Site);
        var team = await AddTeamAsync(harness, "Billing", TeamKind.Team, site.ItemId);
        var today = DateOnly.FromDateTime(CalendarTestHarness.Now);

        await AddWorkerAsync(harness, "Site worker", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = site.ItemId }));
        await AddWorkerAsync(harness, "Team worker", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId }));
        await AddWorkerAsync(harness, "Joins later", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, EffectiveFrom = today.AddDays(7) }));
        await AddWorkerAsync(harness, "Elsewhere");

        // Act
        var (count, items) = await harness.RunAsync(services => services.GetRequiredService<IWorkerManager>().SearchAsync(new WorkerQuery { TeamId = site.ItemId }, 0, 10), commit: false);
        var members = await harness.RunAsync(services => services.GetRequiredService<IWorkerManager>().ListByTeamAsync(team.ItemId, today), commit: false);

        // Assert
        Assert.Equal(3, count);
        Assert.Equal(["Joins later", "Site worker", "Team worker"], items.Select(worker => worker.DisplayName));
        Assert.Equal(["Team worker"], members.Select(worker => worker.DisplayName));
    }

    [Fact]
    public async Task Scope_AManagerManagesTheirTeamsAndBelow_AnAdministratorEverything_AndOthersNothing()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var east = await AddTeamAsync(harness, "East", TeamKind.Region);
        var boston = await AddTeamAsync(harness, "Boston", TeamKind.Site, east.ItemId);
        var west = await AddTeamAsync(harness, "West", TeamKind.Region);

        await AddWorkerAsync(harness, "Manager", worker =>
        {
            worker.UserId = "manager";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = east.ItemId, Role = TeamMemberRole.Manager });
        });

        var bostonWorker = await AddWorkerAsync(harness, "Boston worker", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = boston.ItemId }));
        var westWorker = await AddWorkerAsync(harness, "West worker", worker => worker.TeamMemberships.Add(new TeamMembership { TeamId = west.ItemId }));
        var manager = CalendarTestHarness.User("manager", WorkforcePermissions.ViewWorkers.Name);
        var administrator = CalendarTestHarness.User("admin", WorkforcePermissions.ManageAllWorkforce.Name);
        var stranger = CalendarTestHarness.User("stranger", WorkforcePermissions.ViewWorkers.Name);

        // Act
        var (managerScope, administratorScope, strangerScope, managesBoston, managesWest) = await harness.RunAsync(async services =>
        {
            var scopes = services.GetRequiredService<IWorkforceScopeService>();

            return (
                await scopes.GetManagedScopeAsync(manager),
                await scopes.GetManagedScopeAsync(administrator),
                await scopes.GetManagedScopeAsync(stranger),
                await scopes.ManagesWorkerAsync(manager, bostonWorker),
                await scopes.ManagesWorkerAsync(manager, westWorker));
        }, commit: false);

        // Assert
        Assert.False(managerScope.All);
        Assert.True(managerScope.Covers(east.ItemId));
        Assert.True(managerScope.Covers(boston.ItemId));
        Assert.False(managerScope.Covers(west.ItemId));
        Assert.True(administratorScope.All);
        Assert.Empty(strangerScope.TeamIds);
        Assert.False(strangerScope.All);
        Assert.True(managesBoston);
        Assert.False(managesWest);
    }

    [Fact]
    public async Task ATeamsCalendar_IsReadByItsMembers_AndManagedByTheManagersAbove()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var east = await AddTeamAsync(harness, "East", TeamKind.Region);
        var boston = await AddTeamAsync(harness, "Boston", TeamKind.Site, east.ItemId);

        await AddWorkerAsync(harness, "Member", worker =>
        {
            worker.UserId = "member";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = boston.ItemId });
        });

        await AddWorkerAsync(harness, "Manager", worker =>
        {
            worker.UserId = "manager";
            worker.TeamMemberships.Add(new TeamMembership { TeamId = east.ItemId, Role = TeamMemberRole.Manager });
        });

        // Act
        var (member, manager, stranger, listed) = await harness.RunAsync(async services =>
        {
            var access = services.GetRequiredService<ICalendarAccessService>();
            var calendar = await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, boston.ItemId);
            var memberUser = CalendarTestHarness.User("member", CalendarPermissions.AccessCalendars.Name);

            return (
                await access.GetAccessLevelAsync(memberUser, calendar),
                await access.GetAccessLevelAsync(CalendarTestHarness.User("manager", CalendarPermissions.AccessCalendars.Name), calendar),
                await access.GetAccessLevelAsync(CalendarTestHarness.User("stranger", CalendarPermissions.AccessCalendars.Name), calendar),
                await access.ListAccessibleAsync(memberUser, CalendarAccessLevel.Read, WorkforceConstants.CalendarType));
        }, commit: false);

        // Assert
        Assert.Equal(CalendarAccessLevel.Read, member);
        Assert.Equal(CalendarAccessLevel.Manage, manager);
        Assert.Equal(CalendarAccessLevel.None, stranger);
        Assert.Equal(["Boston"], listed.Select(item => item.Calendar.Name));
    }

    [Fact]
    public async Task ActivityCodes_AreSeededOnce()
    {
        // Arrange
        await using var harness = await CreateAsync();

        // Act
        await harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());
        await harness.RunAsync(services => services.GetRequiredService<ActivityCodeSeeder>().SeedAsync());
        var codes = await harness.RunAsync(services => services.GetRequiredService<IActivityCodeManager>().GetEnabledAsync(), commit: false);

        // Assert
        Assert.Equal(9, codes.Count);
        Assert.Contains(codes, code => code.Kind == ActivityKind.Meal && !code.IsPaid && !code.CountsAsWork);
        Assert.Contains(codes, code => code.Kind == ActivityKind.Break && code.IsPaid && !code.CountsTowardCoverage);
    }

    [Fact]
    public async Task ABreakInsideAPublishedShift_IsBusy_SoSchedulingOffersTheShiftButNotTheBreak()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var team = await AddTeamAsync(harness, "Clinic", TeamKind.Team);
        var teamCalendarId = await harness.RunAsync(async services => (await services.GetRequiredService<ICalendarService>().FindBySourceAsync(WorkforceConstants.Source, team.ItemId)).ItemId, commit: false);
        var worker = await AddWorkerAsync(harness, "Dr. Shift", w =>
        {
            w.UserId = "staff-1";
            w.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId });
        });

        var shift = await harness.CreateEventAsync(teamCalendarId, _day.AddHours(9), _day.AddHours(17), calendarEvent =>
        {
            calendarEvent.EventType = WorkforceConstants.EventTypes.Shift;
            calendarEvent.Attendees.Add(new CalendarAttendee { UserId = "staff-1", Name = "Dr. Shift" });
            calendarEvent.Put(new ShiftData
            {
                WorkerId = worker.ItemId,
                TeamId = team.ItemId,
                Segments =
                [
                    new ShiftSegment { Kind = ActivityKind.Meal, StartUtc = Utc(_day.AddHours(12)), EndUtc = Utc(_day.AddHours(13)) },
                ],
            });
        });

        Assert.True(shift.Succeeded, string.Join(" ", shift.Errors.Select(error => error.Message)));

        var ownCalendar = await harness.CreateCalendarAsync(CalendarTestHarness.General, "Dr. Shift appointments");
        var resource = new SchedulingResource
        {
            Name = "Dr. Shift",
            UserId = "staff-1",
            CalendarId = ownCalendar.ItemId,
            TimeZoneId = "America/New_York",
            AvailabilityMode = SchedulingAvailabilityMode.Shifts,
            ShiftCalendarId = teamCalendarId,
        };

        await harness.RunAsync(services => services.GetRequiredService<SchedulingResourceStore>().CreateAsync(resource).AsTask());

        var type = new AppointmentType
        {
            Name = "Consultation",
            DurationMinutes = 60,
            SlotIntervalMinutes = 60,
            HorizonDays = 30,
            ResourceIds = [resource.ItemId],
        };

        await harness.RunAsync(services => services.GetRequiredService<AppointmentTypeStore>().CreateAsync(type).AsTask());

        // Act
        var slots = await harness.RunAsync(services => services.GetRequiredService<ISchedulingAvailabilityService>().FindSlotsAsync(type, Utc(_day), Utc(_day.AddDays(1))), commit: false);

        // Assert (09:00-17:00 shift with a 12:00-13:00 meal)
        Assert.Equal(["9:00", "10:00", "11:00", "13:00", "14:00", "15:00", "16:00"], slots.Select(slot => CalendarTime.ToLocal(slot.StartUtc, "America/New_York").ToString("H:mm", null)));
    }

    internal static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
    {
        return CalendarTestHarness.CreateAsync(
            services =>
            {
                services.AddSkillsCore();
                services.AddSchedulingCore();
                services.AddWorkforceCore();
                configure?.Invoke(services);
            },
            async schemaBuilder =>
            {
                await SkillsIndexSchema.CreateAsync(schemaBuilder);
                await SchedulingIndexSchema.CreateAsync(schemaBuilder);
                await WorkforceIndexSchema.CreateAsync(schemaBuilder);
            },
            new IIndexProvider[] { new SchedulingResourceIndexProvider(), new AppointmentTypeIndexProvider(), new AppointmentIndexProvider(), new WaitlistEntryIndexProvider() }
                .Concat(SkillsIndexProviders.All())
                .Concat(WorkforceIndexProviders.All())
                .ToArray(),
            [SkillsConstants.CollectionName, SchedulingConstants.CollectionName, WorkforceConstants.CollectionName]);
    }

    internal static Task<WorkforceTeam> AddTeamAsync(CalendarTestHarness harness, string name, TeamKind kind, string parentId = null)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<ITeamManager>();
            var team = await manager.NewAsync();

            team.Name = name;
            team.Kind = kind;
            team.ParentId = parentId;
            team.TimeZoneId = "America/New_York";

            var validation = await manager.ValidateAsync(team);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(team);

            return team;
        });
    }

    internal static Task<WorkerProfile> AddWorkerAsync(CalendarTestHarness harness, string name, Action<WorkerProfile> configure = null)
    {
        return harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IWorkerManager>();
            var worker = await manager.NewAsync();

            worker.DisplayName = name;
            configure?.Invoke(worker);

            var validation = await manager.ValidateAsync(worker);

            Assert.True(validation.Succeeded, string.Join("; ", validation.Errors.Select(error => error.ErrorMessage)));

            await manager.CreateAsync(worker);

            return worker;
        });
    }

    internal static DateTime Utc(DateTime local)
    {
        return CalendarTime.ToUtc(local, "America/New_York");
    }
}
