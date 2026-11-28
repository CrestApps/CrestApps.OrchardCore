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
/// The staff record and capability tools gate on the permission or the staff scope of the matching staff page,
/// write through the staff manager and capability service so their rules decide, keep the editor's rules (user linked
/// once, teams the caller manages, sensitive data), and never return a PIN, its hash or what other modules keep on a
/// staff member. Only <c>terminateStaffMember</c> ends someone's employment, through the staff manager, so the other
/// modules are told; <c>saveStaffMember</c> refuses the Terminated status and a last working day.
/// </summary>
public sealed class StaffManagementToolTests
{
    public static TheoryData<string> ToolNames => new()
    {
        GetStaffMemberTool.TheName,
        SaveStaffMemberTool.TheName,
        TerminateStaffMemberTool.TheName,
        DeleteStaffMemberTool.TheName,
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
    public async Task GetStaffMember_ShowsThePayBasisCurrencySalariesAndCommission_OfThePayCard()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var priya = await WorkforceTests.AddWorkerAsync(harness, "Priya", staff => staff.Put(new StaffPayRates
        {
            PayBasis = StaffPayBasis.Salary,
            Currency = "EUR",
            CommissionRate = 2.5m,
            Salaries =
            [
                new StaffSalary { EffectiveFrom = new DateOnly(2026, 7, 1), AnnualAmount = 64000m },
                new StaffSalary { EffectiveFrom = new DateOnly(2026, 1, 1), AnnualAmount = 60000m },
            ],
        }));
        var hr = CalendarTestHarness.User("hr", StaffPermissions.ViewStaff.Name, StaffPermissions.ManageStaffPay.Name);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(harness, hr, new GetStaffMemberTool(), new() { ["staffMemberId"] = priya.ItemId }));

        // Assert
        var pay = json.GetProperty("pay");
        Assert.Equal("Salary", pay.GetProperty("payBasis").GetString());
        Assert.Equal("EUR", pay.GetProperty("currency").GetString());
        Assert.Equal(2.5m, pay.GetProperty("commissionRate").GetDecimal());
        var salaries = pay.GetProperty("salaries").EnumerateArray().ToList();
        Assert.Equal(["2026-01-01", "2026-07-01"], salaries.Select(salary => salary.GetProperty("effectiveFrom").GetString()));
        Assert.Equal(64000m, salaries[1].GetProperty("annualAmount").GetDecimal());
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
        Assert.Equal($"No staff member you may see has the id '{stranger.ItemId}'. Use getStaffMember with a name to find them.", selfOnly);
    }

    [Fact]
    public async Task GetStaffMember_NeverNamesOrListsPeople_ThatACallerWhoMaySeeOnlyThemselvesCannotSee()
    {
        // Arrange
        await using var harness = await CreateAsync();
        await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        await WorkforceTests.AddWorkerAsync(harness, "Lee Chan");
        await WorkforceTests.AddWorkerAsync(harness, "Lee Self", staff => staff.UserId = "lee-self");
        var self = CalendarTestHarness.User("lee-self", StaffPermissions.ViewOwnStaffProfile.Name);
        var other = CalendarTestHarness.User("other", StaffPermissions.ViewOwnStaffProfile.Name);

        // Act
        var own = AgentToolTestContext.ReadJson(await InvokeAsync(harness, self, new GetStaffMemberTool(), new() { ["name"] = "Lee" }));
        var nobody = await InvokeAsync(harness, other, new GetStaffMemberTool(), new() { ["name"] = "Lee" });
        var missing = await InvokeAsync(harness, other, new GetStaffMemberTool(), new() { ["name"] = "Ghost" });

        // Assert
        Assert.Equal("Lee Self", own.GetProperty("displayName").GetString());
        Assert.Equal("No staff member you may see matches 'Lee'.", nobody);
        Assert.Equal("No staff member you may see matches 'Ghost'.", missing);
        Assert.DoesNotContain("Park", nobody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SaveStaffMemberTool.TheName, false)]
    [InlineData(TerminateStaffMemberTool.TheName, false)]
    [InlineData(DeleteStaffMemberTool.TheName, true)]
    [InlineData(SaveStaffCapabilityTool.TheName, true)]
    [InlineData(DeleteStaffCapabilityTool.TheName, true)]
    public async Task ChangeTool_RefusesACallerWhoMayOnlyViewStaff(string toolName, bool asksForManageStaff)
    {
        // Arrange
        var services = RefusalServices(signedIn: true, authorize: permission => permission.Name == StaffPermissions.ViewStaff.Name);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(Create(toolName), services, new() { ["staffMemberId"] = "x", ["name"] = "x" });

        // Assert
        Assert.Equal(StaffManagementAIToolBase.NotAllowedMessage, result);

        if (asksForManageStaff)
        {
            Assert.Contains(services.GetRequiredService<FakeAuthorizationService>().Checks, check => check.Permission == StaffPermissions.ManageStaff.Name);
        }
    }

    [Fact]
    public async Task SaveStaffMember_RefusesTheTerminatedStatusAndALastWorkingDay_ButCancelsAPlannedOne()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee", staff => staff.TerminationDate = new DateOnly(2026, 12, 31));
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var terminated = await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["staffMemberId"] = lee.ItemId, ["status"] = "Terminated" });
        var lastDay = await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["staffMemberId"] = lee.ItemId, ["terminationDate"] = "2026-11-15" });
        var unchanged = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);
        var cancelled = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new SaveStaffMemberTool(), new() { ["staffMemberId"] = lee.ItemId, ["terminationDate"] = "" }));
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);

        // Assert
        Assert.Equal("Nothing was saved: to end someone's employment use terminateStaffMember, which asks for approval.", terminated);
        Assert.StartsWith("Nothing was saved: a last working day is set with terminateStaffMember", lastDay, StringComparison.Ordinal);
        Assert.Equal(StaffStatus.Active, unchanged.Status);
        Assert.Equal(new DateOnly(2026, 12, 31), unchanged.TerminationDate);
        Assert.Equal("Active", cancelled.GetProperty("status").GetString());
        Assert.Null(stored.TerminationDate);
        Assert.Equal(StaffStatus.Active, stored.Status);
    }

    [Fact]
    public async Task TerminateStaffMember_TerminatesNow_ThroughTheStaffManager_SoTheOtherModulesAreTold()
    {
        // Arrange
        await using var harness = await CreateAsync(services =>
        {
            services.AddSingleton<RecordingLifecycleObserver>();
            services.AddScoped<IStaffLifecycleObserver>(provider => provider.GetRequiredService<RecordingLifecycleObserver>());
        });
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = lee.ItemId }));
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);
        var observer = harness.Services.GetRequiredService<RecordingLifecycleObserver>();

        // Assert
        Assert.True(json.GetProperty("terminated").GetBoolean());
        Assert.Equal("Terminated", json.GetProperty("status").GetString());

        // Today in the site's time zone (noon UTC on 2 November is the morning of the 2nd in New York).
        Assert.Equal("2026-11-02", json.GetProperty("lastWorkingDay").GetString());
        Assert.Equal(StaffStatus.Terminated, stored.Status);
        Assert.Equal(new DateOnly(2026, 11, 2), stored.TerminationDate);
        Assert.Contains(observer.Changes, change => change.StaffMember.ItemId == lee.ItemId && change.Terminated);
    }

    [Fact]
    public async Task TerminateStaffMember_ByName_OnlyListsWhoMatches_AndChangesNothing()
    {
        // Arrange: termination cannot be undone, so a name, even one that matches a single person, never acts.
        await using var harness = await CreateAsync();
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var result = await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["name"] = "Lee Park" });
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);

        // Assert
        Assert.StartsWith("Nothing was changed: A name is not enough to end someone's employment.", result, StringComparison.Ordinal);
        Assert.Contains($"id {lee.ItemId}", result, StringComparison.Ordinal);
        Assert.Equal(StaffStatus.Active, stored.Status);
        Assert.Null(stored.TerminationDate);
    }

    [Fact]
    public async Task TerminateStaffMember_ByEmail_NeedsAnExactAddressOfOnePerson()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park", staff => staff.Email = "lee.park@example.com");
        var leeAnn = await WorkforceTests.AddWorkerAsync(harness, "Lee Ann", staff => staff.Email = "lee.parker@example.com");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act: part of an address matches both people and is refused; the exact address terminates one.
        var partial = await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["email"] = "lee.park" });
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["email"] = "lee.park@example.com" }));
        var leeStored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);
        var leeAnnStored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(leeAnn.ItemId), commit: false);

        // Assert
        Assert.Equal("Nothing was changed: No staff member you may see has the e-mail address 'lee.park'. Give the staffMemberId instead.", partial);
        Assert.Equal(lee.ItemId, json.GetProperty("id").GetString());
        Assert.True(json.GetProperty("terminated").GetBoolean());
        Assert.Equal(StaffStatus.Terminated, leeStored.Status);
        Assert.Equal(StaffStatus.Active, leeAnnStored.Status);
    }

    [Fact]
    public async Task TerminateStaffMember_RecordsALastWorkingDayToCome_WithoutTerminatingYet()
    {
        // Arrange
        await using var harness = await CreateAsync(services =>
        {
            services.AddSingleton<RecordingLifecycleObserver>();
            services.AddScoped<IStaffLifecycleObserver>(provider => provider.GetRequiredService<RecordingLifecycleObserver>());
        });
        var lee = await WorkforceTests.AddWorkerAsync(harness, "Lee Park");
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = lee.ItemId, ["lastWorkingDay"] = "2026-11-30" }));
        var stored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(lee.ItemId), commit: false);
        var observer = harness.Services.GetRequiredService<RecordingLifecycleObserver>();

        // Assert
        Assert.False(json.GetProperty("terminated").GetBoolean());
        Assert.Equal("Active", json.GetProperty("status").GetString());
        Assert.Equal("2026-11-30", json.GetProperty("lastWorkingDay").GetString());
        Assert.Equal(StaffStatus.Active, stored.Status);
        Assert.Equal(new DateOnly(2026, 11, 30), stored.TerminationDate);
        Assert.DoesNotContain(observer.Changes, change => change.Terminated);
    }

    [Fact]
    public async Task TerminateStaffMember_RefusesSomeoneOutsideTheScope_ABadDay_AndSomeoneAlreadyTerminated()
    {
        // Arrange
        await using var harness = await CreateAsync();
        var phoenix = await WorkforceTests.AddTeamAsync(harness, "Phoenix", TeamKind.Team);
        await WorkforceTests.AddWorkerAsync(harness, "Marco", staff =>
        {
            staff.UserId = "marco";
            staff.TeamMemberships.Add(new TeamMembership { TeamId = phoenix.ItemId, Role = TeamMemberRole.Manager });
        });
        var member = await WorkforceTests.AddWorkerAsync(harness, "Lee", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = phoenix.ItemId }));
        var stranger = await WorkforceTests.AddWorkerAsync(harness, "Stranger");
        var gone = await WorkforceTests.AddWorkerAsync(harness, "Gone", staff => staff.TeamMemberships.Add(new TeamMembership { TeamId = phoenix.ItemId }));
        var marco = CalendarTestHarness.User("marco", StaffPermissions.ManageOwnStaff.Name);
        var admin = CalendarTestHarness.User("admin", StaffPermissions.ManageStaff.Name);

        await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = gone.ItemId });

        // Act
        var outsideScope = await InvokeAsync(harness, marco, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = stranger.ItemId });
        var badDay = await InvokeAsync(harness, marco, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = member.ItemId, ["lastWorkingDay"] = "end of the month" });
        var again = AgentToolTestContext.ReadJson(await InvokeAsync(harness, admin, new TerminateStaffMemberTool(), new() { ["staffMemberId"] = gone.ItemId }));
        var strangerStored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(stranger.ItemId), commit: false);
        var memberStored = await harness.RunAsync(async services => await services.GetRequiredService<IStaffMemberManager>().FindByIdAsync(member.ItemId), commit: false);

        // Assert
        Assert.Equal($"Nothing was changed: No staff member you may see has the id '{stranger.ItemId}'. Use getStaffMember with a name to find them.", outsideScope);
        Assert.Equal("Nothing was changed: lastWorkingDay must be a date written as yyyy-MM-dd.", badDay);
        Assert.False(again.GetProperty("terminated").GetBoolean());
        Assert.StartsWith("Nothing was changed: Gone is already terminated.", again.GetProperty("message").GetString(), StringComparison.Ordinal);
        Assert.Equal(StaffStatus.Active, strangerStored.Status);
        Assert.Equal(StaffStatus.Active, memberStored.Status);
        Assert.Null(memberStored.TerminationDate);
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
        Assert.StartsWith("Nothing was saved: The technical name can only have letters", badKey, StringComparison.Ordinal);
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

    private static ServiceProvider RefusalServices(bool signedIn, Func<global::OrchardCore.Security.Permissions.Permission, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize: (permission, _) => authorize?.Invoke(permission) ?? false);
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
            TerminateStaffMemberTool.TheName => new TerminateStaffMemberTool(),
            DeleteStaffMemberTool.TheName => new DeleteStaffMemberTool(),
            ListStaffCapabilitiesTool.TheName => new ListStaffCapabilitiesTool(),
            SaveStaffCapabilityTool.TheName => new SaveStaffCapabilityTool(),
            _ => new DeleteStaffCapabilityTool(),
        };

    private sealed class FakeUserAccessor : IUserAccessor
    {
        public ClaimsPrincipal User { get; set; }
    }

    private sealed class RecordingLifecycleObserver : IStaffLifecycleObserver
    {
        public List<StaffChangedContext> Changes { get; } = [];

        public Task ChangedAsync(StaffChangedContext context, CancellationToken cancellationToken = default)
        {
            Changes.Add(context);

            return Task.CompletedTask;
        }
    }
}
