using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.Core.Services;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.Extensions.DependencyInjection;

namespace CrestApps.OrchardCore.Tests.Staff;

/// <summary>
/// The Staff building block: records are validated (unique user and employee number, a manager that exists and isn't
/// in a loop), changes are kept as history and announced to observers, working staff with an account get the Staff
/// role, the reporting line and manager scope are answered from the index, badges lock after wrong PINs, and a person's
/// time zone falls back to their team's.
/// </summary>
public sealed class StaffServicesTests
{
    [Fact]
    public async Task Validation_RefusesADuplicateEmployeeNumberOrUser_AndAManagerLoop()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var boss = await WorkforceTests.AddWorkerAsync(harness, "Boss", staff => { staff.EmployeeNumber = "E1"; staff.UserId = "boss"; });
        var report = await WorkforceTests.AddWorkerAsync(harness, "Report", staff => staff.ManagerId = boss.ItemId);

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var duplicate = await manager.NewAsync();

            duplicate.DisplayName = "Copy";
            duplicate.EmployeeNumber = " e1 ";
            duplicate.UserId = "boss";

            var loop = await manager.FindByIdAsync(boss.ItemId);

            loop.ManagerId = report.ItemId;

            var self = await manager.FindByIdAsync(report.ItemId);

            self.ManagerId = report.ItemId;

            return (
                Duplicate: (await manager.ValidateAsync(duplicate)).Errors.Select(error => error.ErrorMessage).ToArray(),
                Loop: (await manager.ValidateAsync(loop)).Errors.Select(error => error.ErrorMessage).ToArray(),
                Self: (await manager.ValidateAsync(self)).Errors.Select(error => error.ErrorMessage).ToArray());
        }, commit: false);

        // Assert
        Assert.Contains(errors.Duplicate, error => error.StartsWith("Another staff member has the employee number", StringComparison.Ordinal));
        Assert.Contains(errors.Duplicate, error => error.StartsWith("That user is already the staff member", StringComparison.Ordinal));
        Assert.Contains("That manager reports to this person, directly or through others.", errors.Loop);
        Assert.Contains("A person can't be their own manager.", errors.Self);
    }

    [Fact]
    public async Task Validation_RefusesChangingOrRemovingTheLinkedUser_ButLetsAStaffMemberWithoutOneBeLinked()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var linked = await WorkforceTests.AddWorkerAsync(harness, "Lee", staff => staff.UserId = "lee");
        var unlinked = await WorkforceTests.AddWorkerAsync(harness, "Dana");

        // Act
        var errors = await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var changed = await manager.FindByIdAsync(linked.ItemId);
            var removed = await manager.FindByIdAsync(linked.ItemId);
            var linkedLater = await manager.FindByIdAsync(unlinked.ItemId);

            changed.UserId = "someone-else";
            removed.UserId = null;
            linkedLater.UserId = "dana";

            return (
                Changed: (await manager.ValidateAsync(changed)).Errors.Select(error => error.ErrorMessage).ToArray(),
                Removed: (await manager.ValidateAsync(removed)).Errors.Select(error => error.ErrorMessage).ToArray(),
                LinkedLater: (await manager.ValidateAsync(linkedLater)).Errors.Select(error => error.ErrorMessage).ToArray());
        }, commit: false);

        // Assert
        Assert.Contains(errors.Changed, error => error.StartsWith("A staff member's user account can't be changed or removed", StringComparison.Ordinal));
        Assert.Contains(errors.Removed, error => error.StartsWith("A staff member's user account can't be changed or removed", StringComparison.Ordinal));
        Assert.Empty(errors.LinkedLater);
    }

    [Fact]
    public async Task UnlinkedUserAudience_LeavesOutTheUsersAlreadyLinkedToAStaffMember()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        await WorkforceTests.AddWorkerAsync(harness, "Lee", staff => staff.UserId = "lee");

        // Act
        var excluded = await harness.RunAsync(async services =>
        {
            var audience = services.GetServices<CrestApps.OrchardCore.Users.IUserAudienceProvider>().Single(provider => provider.Name == StaffConstants.UnlinkedUserAudience);

            return (
                Roles: await audience.GetRoleNamesAsync(),
                Excluded: await audience.GetExcludedUserIdsAsync(["lee", "sam"]));
        }, commit: false);

        // Assert
        Assert.Null(excluded.Roles);
        Assert.Equal(["lee"], excluded.Excluded);
    }

    [Fact]
    public async Task Hierarchy_WalksTheManagerChain_AndFindsDirectAndIndirectReports()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var ceo = await WorkforceTests.AddWorkerAsync(harness, "Ceo");
        var director = await WorkforceTests.AddWorkerAsync(harness, "Director", staff => staff.ManagerId = ceo.ItemId);
        var engineer = await WorkforceTests.AddWorkerAsync(harness, "Engineer", staff => staff.ManagerId = director.ItemId);

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var hierarchy = services.GetRequiredService<IStaffHierarchy>();

            return (
                Chain: (await hierarchy.GetManagerChainAsync(engineer.ItemId)).Select(staff => staff.DisplayName).ToArray(),
                Direct: (await hierarchy.GetReportsAsync(ceo.ItemId, includeIndirect: false)).Select(staff => staff.DisplayName).ToArray(),
                All: (await hierarchy.GetReportsAsync(ceo.ItemId)).Select(staff => staff.DisplayName).ToArray(),
                Manages: await hierarchy.ManagesAsync(ceo.ItemId, engineer.ItemId),
                Reverse: await hierarchy.ManagesAsync(engineer.ItemId, ceo.ItemId));
        }, commit: false);

        // Assert
        Assert.Equal(["Director", "Ceo"], result.Chain);
        Assert.Equal(["Director"], result.Direct);
        Assert.Equal(["Director", "Engineer"], result.All);
        Assert.True(result.Manages);
        Assert.False(result.Reverse);
    }

    [Fact]
    public async Task Scope_CoversEveryoneForStaffAdmins_AndReportsAndManagedTeamsForManagers()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var plumbers = await WorkforceTests.AddTeamAsync(harness, "Plumbers", TeamKind.Team);
        var apprentices = await WorkforceTests.AddTeamAsync(harness, "Apprentices", TeamKind.Crew, plumbers.ItemId);
        var manager = await WorkforceTests.AddWorkerAsync(harness, "Manager", staff =>
        {
            staff.UserId = "manager";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = plumbers.ItemId, Role = TeamMemberRole.Manager });
        });
        var report = await WorkforceTests.AddWorkerAsync(harness, "Report", staff => staff.ManagerId = manager.ItemId);
        var apprentice = await WorkforceTests.AddWorkerAsync(harness, "Apprentice", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = apprentices.ItemId }));
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var scopes = services.GetRequiredService<IStaffScopeService>();
            var managerUser = CalendarTestHarness.User("manager", StaffPermissions.ManageOwnStaff.Name);
            var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);
            var nobody = CalendarTestHarness.User("nobody");

            return (
                Report: await scopes.CanManageAsync(managerUser, report),
                Apprentice: await scopes.CanManageAsync(managerUser, apprentice),
                Stranger: await scopes.CanManageAsync(managerUser, stranger),
                Admin: await scopes.CanManageAsync(admin, stranger),
                Nobody: (await scopes.GetScopeAsync(nobody)).IsEmpty);
        }, commit: false);

        // Assert
        Assert.True(result.Report);
        Assert.True(result.Apprentice);
        Assert.False(result.Stranger);
        Assert.True(result.Admin);
        Assert.True(result.Nobody);
    }

    [Fact]
    public async Task Changes_AreKeptAsHistory_AndObserversHearWhoLeft()
    {
        // Arrange
        var observer = new RecordingStaffObserver();
        await using var harness = await WorkforceTests.CreateAsync(services => services.AddSingleton<IStaffLifecycleObserver>(observer));
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", member => member.JobTitle = "Plumber");

        // Act
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(staff.ItemId);

            stored.JobTitle = "Lead plumber";
            stored.Status = StaffStatus.Terminated;
            await manager.UpdateAsync(stored);
        });
        var history = await harness.RunAsync(services => services.GetRequiredService<IStaffHistoryService>().ListAsync(staff.ItemId), commit: false);

        // Assert
        Assert.Contains(history, entry => entry.Field == StaffHistoryFields.Created);
        Assert.Contains(history, entry => entry.Field == StaffHistoryFields.JobTitle && entry.From == "Plumber" && entry.To == "Lead plumber");
        Assert.Contains(history, entry => entry.Field == StaffHistoryFields.Status && entry.To == nameof(StaffStatus.Terminated));
        Assert.True(observer.Changes[0].IsNew);
        Assert.True(observer.Changes[0].Activated);
        Assert.True(observer.Changes[^1].Terminated);
    }

    [Fact]
    public async Task WorkingStaffWithAnAccount_AreInTheStaffRole_AndLeaveItWhenTerminated()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var grants = harness.Services.GetRequiredService<RecordingRoleGrantService>();

        // Act
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Ana", member => member.UserId = "ana");
        var whileWorking = grants.Holds("ana", StaffConstants.DefaultStaffRoleName);

        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(staff.ItemId);

            stored.Status = StaffStatus.Terminated;
            await manager.UpdateAsync(stored);
        });

        // Assert
        Assert.True(whileWorking);
        Assert.False(grants.Holds("ana", StaffConstants.DefaultStaffRoleName));
    }

    [Fact]
    public async Task Badge_AcceptsTheRightPin_AndLocksAfterFiveWrongOnes()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var staff = await WorkforceTests.AddWorkerAsync(harness, "Ana", member => member.EmployeeNumber = "B-7");

        await harness.RunAsync(async services =>
        {
            var store = services.GetRequiredService<StaffMemberStore>();
            var stored = await store.FindByIdAsync(staff.ItemId);

            Assert.Null(services.GetRequiredService<IStaffBadgeService>().SetPin(stored, "4321"));
            await store.UpdateAsync(stored);
        });

        // Act
        var right = await harness.RunAsync(services => services.GetRequiredService<IStaffBadgeService>().VerifyAsync("b-7", "4321"));
        var failures = new List<StaffBadgeFailure>();

        for (var i = 0; i < 6; i++)
        {
            failures.Add((await harness.RunAsync(services => services.GetRequiredService<IStaffBadgeService>().VerifyAsync("B-7", "0000"))).Failure);
        }

        var lockedRight = await harness.RunAsync(services => services.GetRequiredService<IStaffBadgeService>().VerifyAsync("B-7", "4321"));
        var tooShort = await harness.RunAsync(services => Task.FromResult(services.GetRequiredService<IStaffBadgeService>().SetPin(new StaffMember { EmployeeNumber = "X" }, "12")), commit: false);

        // Assert
        Assert.True(right.Succeeded);
        Assert.Equal([StaffBadgeFailure.Invalid, StaffBadgeFailure.Invalid, StaffBadgeFailure.Invalid, StaffBadgeFailure.Invalid, StaffBadgeFailure.Invalid, StaffBadgeFailure.Locked], failures);
        Assert.Equal(StaffBadgeFailure.Locked, lockedRight.Failure);
        Assert.Equal("A PIN is 4 to 8 digits.", tooShort);
    }

    [Fact]
    public async Task TimeZone_IsThePersonsOwn_ElseTheirTeamsOrItsParents_ElseTheSites()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var region = await WorkforceTests.AddTeamAsync(harness, "West", TeamKind.Region);

        await harness.RunAsync(async services =>
        {
            var teams = services.GetRequiredService<ITeamManager>();
            var stored = await teams.FindByIdAsync(region.ItemId);

            stored.TimeZoneId = "America/Los_Angeles";
            await teams.UpdateAsync(stored);
        });

        var crew = await WorkforceTests.AddTeamAsync(harness, "Crew", TeamKind.Crew, region.ItemId);

        await harness.RunAsync(async services =>
        {
            var teams = services.GetRequiredService<ITeamManager>();
            var stored = await teams.FindByIdAsync(crew.ItemId);

            stored.TimeZoneId = null;
            await teams.UpdateAsync(stored);
        });

        var own = await WorkforceTests.AddWorkerAsync(harness, "Own", staff => staff.TimeZoneId = "Europe/Paris");
        var team = await WorkforceTests.AddWorkerAsync(harness, "Team", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = crew.ItemId }));

        // Act
        var zones = await harness.RunAsync(async services =>
        {
            var resolver = services.GetRequiredService<IStaffTimeZoneResolver>();

            return (Own: await resolver.GetTimeZoneIdAsync(own), Team: await resolver.GetTimeZoneIdAsync(team));
        }, commit: false);

        // Assert
        Assert.Equal("Europe/Paris", zones.Own);
        Assert.Equal("America/Los_Angeles", zones.Team);
    }

    [Fact]
    public async Task TeamMembers_IncludeStaffWithoutAnAccount()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var crew = await WorkforceTests.AddTeamAsync(harness, "Crew", TeamKind.Crew);
        var kiosk = await WorkforceTests.AddWorkerAsync(harness, "Kiosk only", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = crew.ItemId }));
        var ana = await WorkforceTests.AddWorkerAsync(harness, "Ana", staff => { staff.UserId = "ana"; staff.TeamMemberships.Add(new TeamMembership { TeamId = crew.ItemId }); });

        // Act
        var result = await harness.RunAsync(services => services.GetRequiredService<ITeamMembershipService>().GetMembersAsync(crew.ItemId), commit: false);

        // Assert
        Assert.Contains(result, member => member.StaffMemberId == kiosk.ItemId && member.UserId is null);
        Assert.Contains(result, member => member.StaffMemberId == ana.ItemId && member.UserId == "ana");
    }

    private sealed class RecordingStaffObserver : IStaffLifecycleObserver
    {
        public List<StaffChangedContext> Changes { get; } = [];

        public Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
        {
            Changes.Add(context);

            return Task.CompletedTask;
        }
    }
}
