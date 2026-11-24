using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using System.Text.Json.Nodes;
using CrestApps.Core.AI.Tooling;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Addresses.Models;
using CrestApps.OrchardCore.AI.Agent.RecordTools;
using CrestApps.OrchardCore.AI.Records;
using CrestApps.OrchardCore.Staff.Services;
using CrestApps.OrchardCore.Teams.AI;
using CrestApps.OrchardCore.Teams.Drivers;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Agent;
using CrestApps.OrchardCore.Tests.Agent.Records;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.DisplayManagement.Handlers;
using OrchardCore.Modules;
using OrchardCore.Security.Permissions;
using TeamsAIToolsStartup = CrestApps.OrchardCore.Teams.AIToolsStartup;
using TeamsPermissions = CrestApps.OrchardCore.Teams.TeamsPermissions;

namespace CrestApps.OrchardCore.Tests.Teams.AI;

/// <summary>
/// Teams are the <c>team</c> kind of the generic record tools: listed in tree order, written through the team editor's
/// drivers and the team manager's rules, and kept when the manager reports blockers. <c>listTeamMembers</c> checks
/// <c>ManageTeams</c> first, asks the membership service for the day in the user's time zone and names people from the
/// staff directory.
/// </summary>
public sealed class TeamToolTests
{
    private readonly List<Team> _teams =
    [
        new Team { ItemId = "org", Name = "Contoso", Kind = TeamKind.Organization },
        new Team { ItemId = "west", Name = "West Region", Kind = TeamKind.Region, ParentId = "org" },
        new Team { ItemId = "pilot", Name = "Pilot Team", Kind = TeamKind.Team, ParentId = "org", TimeZoneId = "America/Phoenix" },
    ];

    private readonly List<string> _actions = [];

    public TeamToolTests()
    {
        _teams[2].Properties["Workforce"] = "kept";
    }

    [Fact]
    public void TheStartup_RegistersListTeamMembers_AndTheTeamKind_WhenTheAIFeatureIsOn()
    {
        // Arrange
        var services = new ServiceCollection();

        new TeamsAIToolsStartup(new RecordTestLocalizer<TeamsAIToolsStartup>()).ConfigureServices(services);

        using var provider = services.BuildServiceProvider();

        // Act
        var tools = provider.GetRequiredService<IOptions<AIToolDefinitionOptions>>().Value.Tools;
        var kinds = provider.GetRequiredService<IOptions<AIRecordKindOptions>>().Value.Kinds;
        var required = typeof(TeamsAIToolsStartup).GetCustomAttributes(typeof(RequireFeaturesAttribute), inherit: false).Cast<RequireFeaturesAttribute>().Single();

        // Assert
        var tool = Assert.Single(tools.Values);
        var kind = Assert.Single(kinds.Values);

        Assert.Equal(ListTeamMembersTool.TheName, tool.Name);
        Assert.False(string.IsNullOrWhiteSpace(tool.Title));
        Assert.NotEqual(tool.Name, tool.Title);
        Assert.False(string.IsNullOrWhiteSpace(tool.Description));
        Assert.Equal("Teams", tool.Category);
        Assert.True(tool.IsSelectable());
        Assert.False(tool.RequiresApproval);
        Assert.Equal(TeamRecordKind.Name, kind.Name);
        Assert.True(kind.CanWrite);
        Assert.Equal(TeamsPermissions.ManageTeams.Name, kind.Permission.Name);
        Assert.Equal(TeamsPermissions.ManageTeams.Name, kind.EffectiveReadPermission.Name);
        Assert.False(string.IsNullOrWhiteSpace(kind.Description));
        Assert.NotEqual(kind.Name, kind.Title);
        Assert.Contains(AIFeatureIds.Area, required.RequiredFeatureNames);
        Assert.DoesNotContain("listTeams", tools.Keys);
        Assert.DoesNotContain("saveTeam", tools.Keys);
        Assert.DoesNotContain("deleteTeam", tools.Keys);
    }

    [Fact]
    public void TeamKind_ReturnsTheCoreFields_AndNeverWhatOtherModulesKeepOnTheTeam()
    {
        // Act
        var json = JsonSerializer.Serialize(TeamRecordKind.Project(_teams[2]));

        // Assert
        Assert.Contains("\"timeZoneId\":\"America/Phoenix\"", json, StringComparison.Ordinal);
        Assert.Contains("\"parentId\":\"org\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain("kept", json, StringComparison.Ordinal);
        Assert.DoesNotContain("properties", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TeamKind_ReturnsThePostalAddressOfTheAddressCard_WithoutItsCoordinates()
    {
        // Arrange
        var team = new Team
        {
            ItemId = "phoenix",
            Name = "Phoenix Site",
            Kind = TeamKind.Site,
            Address = new Address
            {
                AddressLine1 = "100 Main St",
                City = "Phoenix",
                Region = "AZ",
                PostalCode = "85004",
                Country = "US",
                Latitude = 33.45,
                Longitude = -112.07,
            },
        };

        // Act
        var projected = JsonSerializer.SerializeToElement(TeamRecordKind.Project(team));
        var withoutAddress = JsonSerializer.SerializeToElement(TeamRecordKind.Project(_teams[0]));

        // Assert
        var address = projected.GetProperty("address");
        Assert.Equal("100 Main St", address.GetProperty("addressLine1").GetString());
        Assert.Equal("Phoenix", address.GetProperty("city").GetString());
        Assert.Equal("US", address.GetProperty("country").GetString());
        Assert.False(address.TryGetProperty("latitude", out _));
        Assert.Equal(JsonValueKind.Null, withoutAddress.GetProperty("address").ValueKind);
    }

    [Fact]
    public async Task ListRecords_ListsTheTeamsInTreeOrder_FilteredByName()
    {
        // Arrange
        using var services = BuildRecordServices();
        var tool = new ListRecordsTool(RecordToolHarness.Kinds(services));

        // Act
        var all = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(tool, services, new() { ["kind"] = TeamRecordKind.Name }));
        var named = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(tool, services, new() { ["kind"] = TeamRecordKind.Name, ["query"] = "pilot" }));

        // Assert
        Assert.Equal(new[] { "org", "west", "pilot" }, all.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("id").GetString()));
        Assert.Equal(3, all.GetProperty("total").GetInt32());
        Assert.Equal("Region", all.GetProperty("items")[1].GetProperty("kind").GetString());
        Assert.Equal("Pilot Team", Assert.Single(named.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task DeleteRecord_KeepsATeamTheManagerReportsBlockersFor_AndDeletesALeafTeam()
    {
        // Arrange
        using var services = BuildRecordServices();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new DeleteRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TeamRecordKind.Name,
            ["ids"] = new[] { "org", "pilot" },
        }));

        // Assert
        var kept = Assert.Single(json.GetProperty("notDeleted").EnumerateArray());

        Assert.Equal("org", kept.GetProperty("id").GetString());
        Assert.Contains("sub-teams", kept.GetProperty("reason").GetString(), StringComparison.Ordinal);
        Assert.Equal("pilot", Assert.Single(json.GetProperty("deleted").EnumerateArray()).GetProperty("id").GetString());
        Assert.Equal(new[] { "delete" }, _actions);
        Assert.Contains(_teams, team => team.ItemId == "org");
        Assert.DoesNotContain(_teams, team => team.ItemId == "pilot");
    }

    [Fact]
    public async Task SaveRecord_CreatesATeam_ThroughTheTeamEditor_AndTheManagersRules()
    {
        // Arrange
        using var services = BuildRecordServices();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TeamRecordKind.Name,
            ["values"] = JsonNode.Parse("""{ "Name": "Phoenix Crew", "Kind": "Crew", "ParentId": "west", "TimeZoneId": "America/Phoenix" }"""),
        }));

        // Assert
        var created = Assert.Single(_teams, team => team.Name == "Phoenix Crew");

        Assert.True(json.GetProperty("saved").GetBoolean());
        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(TeamKind.Crew, created.Kind);
        Assert.Equal("west", created.ParentId);
        Assert.Equal("America/Phoenix", created.TimeZoneId);
        Assert.Equal("Crew", json.GetProperty("record").GetProperty("kind").GetString());
        Assert.Contains("create", _actions);
    }

    [Fact]
    public async Task SaveRecord_SavesNothing_WhenTheManagersRulesRefuseTheTeam()
    {
        // Arrange
        using var services = BuildRecordServices();

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveRecordTool(RecordToolHarness.Kinds(services)), services, new()
        {
            ["kind"] = TeamRecordKind.Name,
            ["id"] = "west",
            ["values"] = JsonNode.Parse("""{ "ParentId": "west" }"""),
        }));

        // Assert
        Assert.False(json.GetProperty("saved").GetBoolean());
        Assert.Contains(json.GetProperty("errors").EnumerateArray(), error => error.GetProperty("message").GetString().Contains("under itself", StringComparison.Ordinal));
        Assert.DoesNotContain("update", _actions);
        Assert.Equal("org", _teams[1].ParentId);
    }

    [Fact]
    public async Task ListTeamMembers_RefusesACallerWhoIsNotSignedIn_OrLacksManageTeams()
    {
        // Arrange
        var membership = new Mock<ITeamMembershipService>();
        using var anonymous = BuildMemberServices(membership, signedIn: false);
        using var lacking = BuildMemberServices(membership, authorize: (asked, _) => asked.Name != TeamsPermissions.ManageTeams.Name);

        // Act
        var refusedAnonymous = await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), anonymous, new() { ["teamId"] = "org" });
        var refusedLacking = await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), lacking, new() { ["teamId"] = "org" });

        // Assert
        Assert.Equal(TeamsAIToolBase.NotAllowedMessage, refusedAnonymous);
        Assert.Equal(TeamsAIToolBase.NotAllowedMessage, refusedLacking);
        Assert.Equal(TeamsPermissions.ManageTeams.Name, Assert.Single(lacking.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        membership.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ListTeamMembers_ListsTodaysMembersOfTheTeamAndItsSubTeams_WithTheirNames()
    {
        // Arrange
        DateOnly? asked = null;
        var includedSubTeams = false;
        var membership = new Mock<ITeamMembershipService>();

        membership.Setup(service => service.GetMembersAsync("org", It.IsAny<bool>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool subTeams, DateOnly? on, CancellationToken _) =>
            {
                includedSubTeams = subTeams;
                asked = on;
            })
            .ReturnsAsync(new List<TeamMember>
            {
                new() { TeamId = "west", UserId = "u-maya", StaffMemberId = "s-maya", Role = "Manager", Source = "Staff" },
                new() { TeamId = "pilot", StaffMemberId = "s-ben", Role = "Member", EffectiveTo = new DateOnly(2026, 12, 31), Source = "Staff" },
            });

        using var services = BuildMemberServices(membership);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), services, new() { ["teamId"] = "org" }));

        // Assert
        var items = json.GetProperty("items");

        // The day is today in the user's time zone (late on the 19th in Phoenix is already the 20th in UTC).
        Assert.Equal(new DateOnly(2026, 11, 19), asked);
        Assert.True(includedSubTeams);
        Assert.Equal("2026-11-19", json.GetProperty("date").GetString());
        Assert.Equal("Contoso", json.GetProperty("team").GetProperty("name").GetString());
        Assert.Equal(2, json.GetProperty("total").GetInt32());
        Assert.False(json.GetProperty("truncated").GetBoolean());
        Assert.Equal("Ben Ortiz", items[0].GetProperty("name").GetString());
        Assert.Equal("Pilot Team", items[0].GetProperty("teamName").GetString());
        Assert.Equal("2026-12-31", items[0].GetProperty("effectiveTo").GetString());
        Assert.Equal("Maya Lopez", items[1].GetProperty("name").GetString());
        Assert.Equal("Manager", items[1].GetProperty("role").GetString());
        Assert.Equal("West Region", items[1].GetProperty("teamName").GetString());
        Assert.Equal("u-maya", items[1].GetProperty("userId").GetString());
    }

    [Fact]
    public async Task ListTeamMembers_TakesADayOrEveryMembership_AndRefusesAnUnknownTeamOrABadDay()
    {
        // Arrange
        var calls = new List<(bool, DateOnly?)>();
        var membership = new Mock<ITeamMembershipService>();

        membership.Setup(service => service.GetMembersAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
            .Callback((string _, bool subTeams, DateOnly? on, CancellationToken _) => calls.Add((subTeams, on)))
            .ReturnsAsync(new List<TeamMember>());

        using var services = BuildMemberServices(membership);

        // Act
        var everyDay = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), services, new() { ["teamId"] = "west", ["allDates"] = true }));
        var oneDay = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), services, new() { ["teamId"] = "west", ["date"] = "2026-12-01", ["includeSubTeams"] = false }));
        var unknown = await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), services, new() { ["teamId"] = "ghost" });
        var badDay = await AgentToolTestContext.InvokeAsync(new ListTeamMembersTool(), services, new() { ["teamId"] = "west", ["date"] = "next week" });

        // Assert
        Assert.Equal(new (bool, DateOnly?)[] { (true, null), (false, new DateOnly(2026, 12, 1)) }, calls);
        Assert.True(everyDay.GetProperty("allDates").GetBoolean());
        Assert.Equal(0, everyDay.GetProperty("total").GetInt32());
        Assert.Equal("2026-12-01", oneDay.GetProperty("date").GetString());
        Assert.Equal("There is no team with the id 'ghost'. Use listRecords with kind team to find it.", unknown);
        Assert.Equal("The date must be a day written as yyyy-MM-dd.", badDay);
    }

    private ServiceProvider BuildRecordServices()
    {
        var services = AgentToolTestContext.CreateServices();
        var timeZones = new Mock<ITimeZoneSelectListProvider>();

        timeZones.Setup(provider => provider.GetTimeZoneSelectListAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { new KeyValuePair<string, string>("America/Phoenix", "Phoenix"), new KeyValuePair<string, string>("UTC", "UTC") });

        services.AddLogging();
        services.AddLocalization();
        services.AddMvcCore().AddDataAnnotations();
        services.AddEditorDisplayManagement();
        services.AddScoped<AIRecordScopeChanges>();
        services.AddScoped<IDisplayDriver<Team>, TeamDisplayDriver>();
        services.AddSingleton(timeZones.Object);
        services.AddSingleton(CreateTeamManager().Object);

        new TeamsAIToolsStartup(new RecordTestLocalizer<TeamsAIToolsStartup>()).ConfigureServices(services);

        return services.BuildServiceProvider();
    }

    private ServiceProvider BuildMemberServices(Mock<ITeamMembershipService> membership, bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var directory = new Mock<IStaffDirectory>();
        var localClock = new Mock<ILocalClock>();

        directory.Setup(service => service.GetNamesAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["s-maya"] = "Maya Lopez",
                ["s-ben"] = "Ben Ortiz",
            });
        localClock.Setup(clock => clock.GetLocalNowAsync())
            .ReturnsAsync(new DateTimeOffset(2026, 11, 19, 23, 30, 0, TimeSpan.FromHours(-7)));

        services.AddSingleton(CreateTeamManager().Object);
        services.AddSingleton(membership.Object);
        services.AddSingleton(directory.Object);
        services.AddSingleton(localClock.Object);

        return services.BuildServiceProvider();
    }

    private Mock<ITeamManager> CreateTeamManager()
    {
        var manager = new Mock<ITeamManager>();

        manager.Setup(value => value.ListTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [.. Order(null, 0)]);
        manager.Setup(value => value.GetSubtreeIdsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string teamId, CancellationToken _) => new HashSet<string>(Subtree(teamId), StringComparer.Ordinal));
        manager.Setup(value => value.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns((string id, CancellationToken _) => ValueTask.FromResult(_teams.FirstOrDefault(team => team.ItemId == id)));
        manager.Setup(value => value.NewAsync(It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((JsonNode _, CancellationToken _) => ValueTask.FromResult(new Team { ItemId = "new-1" }));
        manager.Setup(value => value.ValidateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns((Team team, CancellationToken _) =>
            {
                _actions.Add("validate");

                var result = new ValidationResultDetails();

                if (string.IsNullOrEmpty(team.Name))
                {
                    result.Fail(new ValidationResult("The name is required.", [nameof(Team.Name)]));
                }

                for (var current = team.ParentId; current is not null; current = _teams.FirstOrDefault(item => item.ItemId == current)?.ParentId)
                {
                    if (current == team.ItemId)
                    {
                        result.Fail(new ValidationResult("A team can't be placed under itself or under one of its own teams.", [nameof(Team.ParentId)]));

                        break;
                    }
                }

                return ValueTask.FromResult(result);
            });
        manager.Setup(value => value.CreateAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns((Team team, CancellationToken _) =>
            {
                _actions.Add("create");
                _teams.Add(team);

                return ValueTask.CompletedTask;
            });
        manager.Setup(value => value.UpdateAsync(It.IsAny<Team>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
            .Returns((Team _, JsonNode _, CancellationToken _) =>
            {
                _actions.Add("update");

                return ValueTask.CompletedTask;
            });
        manager.Setup(value => value.DeleteAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .Returns((Team team, CancellationToken _) =>
            {
                _actions.Add("delete");

                return ValueTask.FromResult(_teams.Remove(team));
            });
        manager.Setup(value => value.GetDeletionBlockersAsync(It.IsAny<Team>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Team team, CancellationToken _) => _teams.Any(child => child.ParentId == team.ItemId)
                ? ["The team still has sub-teams. Move or delete them first."]
                : []);

        return manager;
    }

    private IEnumerable<(Team Team, int Depth)> Order(string parentId, int depth)
    {
        foreach (var team in _teams.Where(team => team.ParentId == parentId))
        {
            yield return (team, depth);

            foreach (var child in Order(team.ItemId, depth + 1))
            {
                yield return child;
            }
        }
    }

    private IEnumerable<string> Subtree(string teamId)
    {
        yield return teamId;

        foreach (var child in _teams.Where(team => team.ParentId == teamId))
        {
            foreach (var id in Subtree(child.ItemId))
            {
                yield return id;
            }
        }
    }
}
