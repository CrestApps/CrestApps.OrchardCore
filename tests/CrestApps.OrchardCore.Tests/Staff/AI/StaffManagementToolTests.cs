using System.Security.Claims;
using CrestApps.Core;
using CrestApps.Core.Security;
using CrestApps.OrchardCore.Skills.Models;
using CrestApps.OrchardCore.Skills.Services;
using CrestApps.OrchardCore.Staff;
using CrestApps.OrchardCore.Staff.AI;
using CrestApps.OrchardCore.Staff.Core;
using CrestApps.OrchardCore.Staff.Models;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Calendars;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Workforce;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;
using OrchardCore.Users.Models;

namespace CrestApps.OrchardCore.Tests.Staff.AI;

/// <summary>
/// The staff record, badge and capability tools gate on the permission or the staff scope of the matching staff page,
/// write through the staff manager and capability service so their rules decide, keep the editor's rules (user linked
/// once, teams the caller manages, sensitive data), and never return a PIN, its hash or what other modules keep on a
/// staff member.
/// </summary>
public sealed class StaffManagementToolTests
{
    public static TheoryData<string> ToolNames => new()
    {
        GetStaffMemberTool.TheName,
        SaveStaffMemberTool.TheName,
        DeleteStaffMemberTool.TheName,
        SetStaffBadgePinTool.TheName,
        ListStaffCapabilitiesTool.TheName,
        SaveStaffCapabilityTool.TheName,
        DeleteStaffCapabilityTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Arrange
        var services = RefusalServices(signedIn: false);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["staffMemberId"] = "x" });

        // Assert
        Assert.Equal(StaffManagementAIToolBase.NotAllowedMessage, result);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutThePermissionOrScopeOfItsPage(string toolName)
    {
        // Arrange
        var services = RefusalServices(signedIn: true);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["staffMemberId"] = "x" });

        // Assert
        Assert.Equal(StaffManagementAIToolBase.NotAllowedMessage, result);
    }

    [Fact]
    public async Task SaveAndGetStaffMember_AddsAPersonToATeam_UnderAManager_AndFindsThemByName()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var crew = await WorkforceTests.AddTeamAsync(harness, "Phoenix Service Crew", TeamKind.Crew);
        var marco = await WorkforceTests.AddWorkerAsync(harness, "Marco Ruiz");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var saved = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new()
        {
            ["displayName"] = "Priya Shah",
            ["jobTitle"] = "Field Technician",
            ["employmentType"] = "FullTime",
            ["hireDate"] = "2026-12-01",
            ["managerId"] = marco.ItemId,
            ["teamMemberships"] = new[] { new Dictionary<string, object> { ["teamId"] = crew.ItemId, ["role"] = "Member" } },
        }));
        var found = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new GetStaffMemberTool(), new() { ["name"] = "priya shah" }));

        // Assert
        Assert.True(saved.GetProperty("created").GetBoolean());
        Assert.Equal(saved.GetProperty("id").GetString(), found.GetProperty("id").GetString());
        Assert.Equal("Marco Ruiz", found.GetProperty("manager").GetProperty("name").GetString());
        Assert.Equal("Phoenix Service Crew", found.GetProperty("mainTeam").GetProperty("name").GetString());
        Assert.Equal("2026-12-01", found.GetProperty("hireDate").GetString());
        Assert.Equal("Created", Assert.Single(found.GetProperty("history").EnumerateArray()).GetProperty("field").GetString());
    }

    [Fact]
    public async Task SaveStaffMember_RefusesWhatTheEditorRefuses()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var phoenix = await WorkforceTests.AddTeamAsync(harness, "Phoenix", TeamKind.Team);
        var tucson = await WorkforceTests.AddTeamAsync(harness, "Tucson", TeamKind.Team);
        await WorkforceTests.AddWorkerAsync(harness, "Marco", staff =>
        {
            staff.UserId = "marco";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = phoenix.ItemId, Role = TeamMemberRole.Manager });
        });
        var member = await WorkforceTests.AddWorkerAsync(harness, "Lee", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = phoenix.ItemId }));
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");
        var marco = CalendarTestHarness.User("marco", StaffPermissions.ManageOwnStaff.Name);

        // Act
        var outsideScope = await InvokeAsync(harness, marco, new SaveStaffMemberTool(), new() { ["staffMemberId"] = stranger.ItemId, ["jobTitle"] = "Boss" });
        var otherTeam = await InvokeAsync(harness, marco, new SaveStaffMemberTool(), new()
        {
            ["staffMemberId"] = member.ItemId,
            ["teamMemberships"] = new[] { new Dictionary<string, object> { ["teamId"] = phoenix.ItemId }, new Dictionary<string, object> { ["teamId"] = tucson.ItemId } },
        });
        var birthday = await InvokeAsync(harness, marco, new SaveStaffMemberTool(), new() { ["staffMemberId"] = member.ItemId, ["dateOfBirth"] = "1990-01-01" });
        var ownManager = await InvokeAsync(harness, marco, new SaveStaffMemberTool(), new() { ["staffMemberId"] = member.ItemId, ["managerId"] = member.ItemId });
        var changed = AgentToolTestContext.ReadJson(await InvokeAsync(harness, marco, new SaveStaffMemberTool(), new() { ["staffMemberId"] = member.ItemId, ["jobTitle"] = "Lead plumber" }));

        // Assert
        Assert.StartsWith("Nothing was saved: you are not allowed to change Stranger", outsideScope, StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: you can only add people to the teams you manage.", otherTeam);
        Assert.StartsWith("Nothing was saved: you are not allowed to change the date of birth", birthday, StringComparison.Ordinal);
        Assert.Equal("Nothing was saved: A person can't be their own manager.", ownManager);
        Assert.Equal("Lead plumber", changed.GetProperty("jobTitle").GetString());
    }

    [Fact]
    public async Task SaveStaffMember_LinksAUserOnce_AndTakesTheirName()
    {
        // Arrange
        await using var harness = await CreateAsync();
        harness.Services.GetRequiredService<InMemoryUserDirectory>().Set("u-jsmith", "Jane Smith", "jane@example.com");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var created = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["userName"] = "jsmith", ["displayName"] = "Typed Name" }));
        var relinked = await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["staffMemberId"] = created.GetProperty("id").GetString(), ["userName"] = "other" });
        var unknown = await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["userName"] = "ghost" });

        // Assert
        Assert.Equal("u-jsmith", created.GetProperty("userId").GetString());
        Assert.Equal("Jane Smith", created.GetProperty("displayName").GetString());
        Assert.Equal("displayName", Assert.Single(created.GetProperty("ignoredFields").EnumerateArray()).GetString());
        Assert.StartsWith("Nothing was saved: a staff member's user account can't be changed once linked", relinked, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: there is no user 'ghost'.", unknown, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GetStaffMember_ShowsSensitiveDataAndPayOnlyWithTheirPermissions_AndNeverTheBadge()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var priya = await WorkforceTests.AddWorkerAsync(harness, "Priya", staff =>
        {
            staff.EmployeeNumber = "E-100";
            staff.DateOfBirth = new DateOnly(1991, 4, 5);
            staff.HomeBase = new StaffLocation { Address = "12 Home Street" };
            staff.Put(new StaffPayRates { HourlyCost = 40m, Rates = [new StaffPayRate { EffectiveFrom = new DateOnly(2026, 1, 1), HourlyRate = 31.5m }] });
        });
        await harness.RunAsync(async services =>
        {
            var manager = services.GetRequiredService<IStaffMemberManager>();
            var stored = await manager.FindByIdAsync(priya.ItemId);

            Assert.Null(services.GetRequiredService<IStaffBadgeService>().SetPin(stored, "4821"));
            await manager.UpdateAsync(stored);
        });
        var hash = await harness.RunAsync(async services => (await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(priya.ItemId)).TryGet<StaffBadge>(out var badge) ? badge.PinHash : null, commit: false);
        var viewer = CalendarTestHarness.User("viewer", StaffPermissions.ViewStaff.Name);
        var hr = CalendarTestHarness.User("hr", StaffPermissions.ViewStaff.Name, StaffPermissions.ViewSensitiveStaffData.Name, StaffPermissions.ManageStaffPay.Name);

        // Act
        var plain = await InvokeAsync(harness, viewer, new GetStaffMemberTool(), new() { ["staffMemberId"] = priya.ItemId });
        var full = await InvokeAsync(harness, hr, new GetStaffMemberTool(), new() { ["staffMemberId"] = priya.ItemId });

        // Assert
        var plainJson = AgentToolTestContext.ReadJson(plain);
        var fullJson = AgentToolTestContext.ReadJson(full);

        Assert.False(plainJson.TryGetProperty("dateOfBirth", out _));
        Assert.False(plainJson.TryGetProperty("pay", out _));
        Assert.Equal("1991-04-05", fullJson.GetProperty("dateOfBirth").GetString());
        Assert.Equal(31.5m, Assert.Single(fullJson.GetProperty("pay").GetProperty("rates").EnumerateArray()).GetProperty("hourlyRate").GetDecimal());
        Assert.True(fullJson.GetProperty("hasHomeBase").GetBoolean());
        Assert.False(string.IsNullOrEmpty(hash));

        foreach (var result in new[] { plain, full })
        {
            Assert.DoesNotContain(hash, result, StringComparison.Ordinal);
            Assert.DoesNotContain("PinHash", result, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("12 Home Street", result, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task GetStaffMember_ListsCandidates_WhenANameIsAmbiguous_AndRefusesSomeoneOutsideTheScope()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        await WorkforceTests.AddWorkerAsync(harness, "Lee Chan");
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");
        await WorkforceTests.AddWorkerAsync(harness, "Boss", staff => staff.UserId = "boss");
        var viewer = CalendarTestHarness.User("viewer", StaffPermissions.ViewStaff.Name);
        var boss = CalendarTestHarness.User("boss", StaffPermissions.ManageOwnStaff.Name);

        // Act
        var ambiguous = await InvokeAsync(harness, viewer, new GetStaffMemberTool(), new() { ["name"] = "Lee" });

        // ManageOwnStaff implies ViewStaff, so a manager with only it sees everyone, as on the staff pages.
        var managerView = AgentToolTestContext.ReadJson(await InvokeAsync(harness, boss, new GetStaffMemberTool(), new() { ["staffMemberId"] = stranger.ItemId }));
        var selfOnly = await InvokeAsync(harness, CalendarTestHarness.User("someone", StaffPermissions.ViewOwnStaffProfile.Name), new GetStaffMemberTool(), new() { ["staffMemberId"] = stranger.ItemId });

        // Assert
        Assert.StartsWith("Several staff members match 'Lee':", ambiguous, StringComparison.Ordinal);
        Assert.Equal("Stranger", managerView.GetProperty("displayName").GetString());
        Assert.StartsWith("You are not allowed to see Stranger's record", selfOnly, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteStaffMember_IsRefusedWhilePeopleReportToThePerson()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var boss = await WorkforceTests.AddWorkerAsync(harness, "Boss");
        var report = await WorkforceTests.AddWorkerAsync(harness, "Report", staff => staff.ManagerId = boss.ItemId);
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var refused = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new DeleteStaffMemberTool(), new() { ["staffMemberId"] = boss.ItemId }));
        var deleted = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new DeleteStaffMemberTool(), new() { ["staffMemberId"] = report.ItemId }));
        var gone = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(report.ItemId), commit: false);

        // Assert
        Assert.False(refused.GetProperty("deleted").GetBoolean());
        Assert.Equal("Report", Assert.Single(refused.GetProperty("directReports").EnumerateArray()).GetProperty("name").GetString());
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Null(gone);
    }

    [Fact]
    public async Task SetStaffBadgePin_ClearsAPinOnlyForSomeoneInTheScope_AndNeverTakesOne()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var team = await WorkforceTests.AddTeamAsync(harness, "Store", TeamKind.Team);
        await WorkforceTests.AddWorkerAsync(harness, "Manager", staff =>
        {
            staff.UserId = "manager";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Manager });
        });
        var cashier = await WorkforceTests.AddWorkerAsync(harness, "Cashier", staff =>
        {
            staff.EmployeeNumber = "E-1";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId });
        });
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger", staff => staff.EmployeeNumber = "E-2");

        foreach (var id in new[] { cashier.ItemId, stranger.ItemId })
        {
            await harness.RunAsync(async services =>
            {
                var manager = services.GetRequiredService<IStaffMemberManager>();
                var stored = await manager.FindByIdAsync(id);

                Assert.Null(services.GetRequiredService<IStaffBadgeService>().SetPin(stored, "4821"));
                await manager.UpdateAsync(stored);
            });
        }

        var caller = CalendarTestHarness.User("manager", StaffPermissions.ManageOwnStaff.Name);

        // Act
        var status = AgentToolTestContext.ReadJson(await InvokeAsync(harness, caller, new SetStaffBadgePinTool(), new() { ["staffMemberId"] = cashier.ItemId }));
        var pin = await InvokeAsync(harness, caller, new SetStaffBadgePinTool(), new() { ["staffMemberId"] = cashier.ItemId, ["pin"] = "1111" });
        var outside = await InvokeAsync(harness, caller, new SetStaffBadgePinTool(), new() { ["staffMemberId"] = stranger.ItemId, ["clearPin"] = true });
        var cleared = AgentToolTestContext.ReadJson(await InvokeAsync(harness, caller, new SetStaffBadgePinTool(), new() { ["staffMemberId"] = cashier.ItemId, ["clearPin"] = true }));
        var hasPin = await harness.RunAsync(async services => services.GetRequiredService<IStaffBadgeService>().HasPin(await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(cashier.ItemId)), commit: false);

        // Assert
        Assert.True(status.GetProperty("hasPin").GetBoolean());
        Assert.StartsWith("Nothing was done: 'pin' is not an argument of setStaffBadgePin.", pin, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was changed: you are not allowed to change Stranger's badge", outside, StringComparison.Ordinal);
        Assert.True(cleared.GetProperty("cleared").GetBoolean());
        Assert.False(hasPin);
    }

    [Fact]
    public async Task CapabilityTools_AddChangeAndDeleteACustomCapability_ThroughTheService()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var added = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new SaveStaffCapabilityTool(), new() { ["name"] = "Dispatcher", ["isNew"] = true, ["displayName"] = "Dispatcher" }));
        var badKey = await InvokeAsync(harness, admin, new SaveStaffCapabilityTool(), new() { ["name"] = "9 bad", ["isNew"] = true, ["displayName"] = "Bad" });
        var changed = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new SaveStaffCapabilityTool(), new() { ["name"] = "dispatcher", ["enabled"] = false }));
        var listed = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new ListStaffCapabilitiesTool(), []));
        var deleted = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new DeleteStaffCapabilityTool(), new() { ["name"] = "Dispatcher" }));
        var missing = await InvokeAsync(harness, admin, new DeleteStaffCapabilityTool(), new() { ["name"] = "Dispatcher" });

        // Assert
        Assert.True(added.GetProperty("created").GetBoolean());
        Assert.Equal(100, added.GetProperty("position").GetInt32());
        Assert.StartsWith("Nothing was saved: The key can only have letters", badKey, StringComparison.Ordinal);
        Assert.False(changed.GetProperty("enabled").GetBoolean());
        Assert.Equal("Dispatcher", changed.GetProperty("displayName").GetString());

        var dispatcher = Assert.Single(listed.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == "Dispatcher");

        Assert.False(dispatcher.GetProperty("enabled").GetBoolean());
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.StartsWith("Nothing was deleted: there is no capability 'Dispatcher'.", missing, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CapabilityTools_SayAChangeIsSeenFromTheNextReply_WithinOneMessage()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var (again, note) = await harness.RunAsync(async services =>
        {
            services.GetRequiredService<FakeUserAccessor>().User = admin;
            await AgentToolTestContext.InvokeAsync(new SaveStaffCapabilityTool(), services, new() { ["name"] = "Dispatcher", ["isNew"] = true, ["displayName"] = "Dispatcher" });

            var second = await AgentToolTestContext.InvokeAsync(new SaveStaffCapabilityTool(), services, new() { ["name"] = "Dispatcher", ["enabled"] = false });
            var list = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListStaffCapabilitiesTool(), services, []));

            return (second, list.GetProperty("note").GetString());
        });

        // Assert
        Assert.StartsWith("Nothing was saved: the capability 'Dispatcher' was changed earlier in this message.", again, StringComparison.Ordinal);
        Assert.Contains("from the next reply", note, StringComparison.Ordinal);
    }

    [Fact]
    public async Task StaffSkillSubjectProvider_NamesStaff_AndAllowsOnlyThePeopleInTheCallersScope()
    {
        // Arrange
        await using var harness = await CreateAsync(services => services.AddStaffSkillLinks());
        var team = await WorkforceTests.AddTeamAsync(harness, "Crew", TeamKind.Crew);
        await WorkforceTests.AddWorkerAsync(harness, "Manager", staff =>
        {
            staff.UserId = "manager";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId, Role = TeamMemberRole.Manager });
        });
        var member = await WorkforceTests.AddWorkerAsync(harness, "Luis", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = team.ItemId }));
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");
        var caller = CalendarTestHarness.User("manager", StaffPermissions.ManageOwnStaff.Name);

        // Act
        var result = await harness.RunAsync(async services =>
        {
            var provider = Assert.Single(services.GetServices<ISkillSubjectProvider>(), item => item.Kind == SkillSubjectKind.Staff);

            return (
                Names: await provider.GetNamesAsync([member.ItemId, "ghost"]),
                Member: await provider.CanManageAsync(caller, member.ItemId),
                Stranger: await provider.CanManageAsync(caller, stranger.ItemId));
        }, commit: false);

        // Assert
        Assert.Equal("Luis", Assert.Single(result.Names).Value);
        Assert.True(result.Member);
        Assert.False(result.Stranger);
    }

    private static Task<string> InvokeAsync(CalendarTestHarness harness, ClaimsPrincipal caller, AIFunction tool, Dictionary<string, object> arguments)
        => harness.RunAsync(services =>
        {
            services.GetRequiredService<FakeUserAccessor>().User = caller;

            return AgentToolTestContext.InvokeAsync(tool, services, arguments);
        });

    private static Task<CalendarTestHarness> CreateAsync(Action<IServiceCollection> configure = null)
        => WorkforceTests.CreateAsync(services =>
        {
            services.AddSingleton<FakeUserAccessor>();
            services.AddSingleton<IUserAccessor>(provider => provider.GetRequiredService<FakeUserAccessor>());
            services.AddScoped<StaffCapabilityToolScope>();

            var localClock = new Mock<ILocalClock>();
            localClock.Setup(clock => clock.ConvertToLocalAsync(It.IsAny<DateTimeOffset>())).ReturnsAsync((DateTimeOffset value) => value);
            services.AddSingleton(localClock.Object);

            var jsmith = new User { UserId = "u-jsmith", UserName = "jsmith", Email = "jane@example.com" };
            var other = new User { UserId = "u-other", UserName = "other" };
            var userManager = new Mock<UserManager<IUser>>(Mock.Of<IUserStore<IUser>>(), null, null, null, null, null, null, null, null);
            userManager.Setup(manager => manager.FindByNameAsync("jsmith")).ReturnsAsync(jsmith);
            userManager.Setup(manager => manager.FindByNameAsync("other")).ReturnsAsync(other);
            userManager.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>())).ReturnsAsync((IUser user) => ((User)user).UserId);
            services.AddSingleton(userManager.Object);

            configure?.Invoke(services);
        });

    private static ServiceProvider RefusalServices(bool signedIn)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize: (_, _) => false);
        var scope = new Mock<IStaffScopeService>();

        scope.Setup(service => service.GetScopeAsync(It.IsAny<ClaimsPrincipal>(), It.IsAny<CancellationToken>())).ReturnsAsync(StaffScope.None);
        services.AddSingleton(scope.Object);

        return services.BuildServiceProvider();
    }

    private static AIFunction Create(string toolName)
        => toolName switch
        {
            GetStaffMemberTool.TheName => new GetStaffMemberTool(),
            SaveStaffMemberTool.TheName => new SaveStaffMemberTool(),
            DeleteStaffMemberTool.TheName => new DeleteStaffMemberTool(),
            SetStaffBadgePinTool.TheName => new SetStaffBadgePinTool(),
            ListStaffCapabilitiesTool.TheName => new ListStaffCapabilitiesTool(),
            SaveStaffCapabilityTool.TheName => new SaveStaffCapabilityTool(),
            _ => new DeleteStaffCapabilityTool(),
        };

    private sealed class FakeUserAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }
    }
}
