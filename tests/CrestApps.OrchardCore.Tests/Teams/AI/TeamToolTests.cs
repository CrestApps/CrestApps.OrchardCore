using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Teams;
using CrestApps.OrchardCore.Teams.AI;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Tests.Agent;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Security.Permissions;

namespace CrestApps.OrchardCore.Tests.Teams.AI;

/// <summary>
/// The team tools check <c>ManageTeams</c> first, write through the team manager so its rules decide, change only the
/// core fields given, and refuse to delete a team the manager reports blockers for.
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

    public static TheoryData<string> ToolNames => new()
    {
        ListTeamsTool.TheName,
        SaveTeamTool.TheName,
        DeleteTeamTool.TheName,
    };

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWhoIsNotSignedIn(string toolName)
    {
        // Act
        var result = await InvokeAsync(toolName, Build(signedIn: false), Arguments(toolName));

        // Assert
        Assert.Equal(TeamsAIToolBase.NotAllowedMessage, result);
        Assert.Empty(_actions);
    }

    [Theory]
    [MemberData(nameof(ToolNames))]
    public async Task Tool_RefusesACallerWithoutManageTeams(string toolName)
    {
        // Arrange
        var services = Build(authorize: (asked, _) => asked.Name != TeamsPermissions.ManageTeams.Name);

        // Act
        var result = await InvokeAsync(toolName, services, Arguments(toolName));

        // Assert
        Assert.Equal(TeamsAIToolBase.NotAllowedMessage, result);
        Assert.Equal(TeamsPermissions.ManageTeams.Name, Assert.Single(services.GetRequiredService<FakeAuthorizationService>().Checks).Permission);
        Assert.Empty(_actions);
    }

    [Fact]
    public async Task ListTeams_ListsTheTree_WithTheParentsName_FilteredByName()
    {
        // Act
        var all = AgentToolTestContext.ReadJson(await InvokeAsync(ListTeamsTool.TheName, Build(), new()));
        var named = AgentToolTestContext.ReadJson(await InvokeAsync(ListTeamsTool.TheName, Build(), new() { ["name"] = "pilot" }));

        // Assert
        var pilot = Assert.Single(named.GetProperty("items").EnumerateArray());

        Assert.Equal(3, all.GetProperty("total").GetInt32());
        Assert.Equal("Contoso", pilot.GetProperty("parentName").GetString());
        Assert.Equal(1, pilot.GetProperty("depth").GetInt32());
        Assert.Equal("America/Phoenix", pilot.GetProperty("timeZoneId").GetString());
    }

    [Fact]
    public async Task SaveTeam_CreatesATeamUnderItsParent()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await InvokeAsync(SaveTeamTool.TheName, Build(), new()
        {
            ["name"] = "Phoenix Service Crew",
            ["kind"] = "crew",
            ["parentId"] = "west",
            ["timeZoneId"] = "America/Phoenix",
        }));

        // Assert
        var created = _teams.Last();

        Assert.True(json.GetProperty("created").GetBoolean());
        Assert.Equal(new[] { "validate", "create" }, _actions);
        Assert.Equal(TeamKind.Crew, created.Kind);
        Assert.Equal("west", created.ParentId);
        Assert.Equal("West Region", json.GetProperty("team").GetProperty("parentName").GetString());
    }

    [Fact]
    public async Task SaveTeam_ChangesOnlyTheGivenFields_AndKeepsWhatOtherModulesStore()
    {
        // Act
        await InvokeAsync(SaveTeamTool.TheName, Build(), new() { ["teamId"] = "pilot", ["enabled"] = false });

        // Assert
        var pilot = _teams[2];

        Assert.Equal(new[] { "validate", "update" }, _actions);
        Assert.False(pilot.Enabled);
        Assert.Equal("Pilot Team", pilot.Name);
        Assert.Equal("America/Phoenix", pilot.TimeZoneId);
        Assert.Equal("kept", pilot.Properties["Workforce"]);
    }

    [Fact]
    public async Task SaveTeam_SavesNothing_WhenTheManagersRulesFail_OrTheKindIsUnknown()
    {
        // Act
        var underItself = await InvokeAsync(SaveTeamTool.TheName, Build(), new() { ["teamId"] = "org", ["parentId"] = "west" });
        var kind = await InvokeAsync(SaveTeamTool.TheName, Build(), new() { ["name"] = "Squad", ["kind"] = "Squad" });
        var missing = await InvokeAsync(SaveTeamTool.TheName, Build(), new() { ["teamId"] = "ghost", ["name"] = "Ghost" });

        // Assert
        Assert.Equal("Nothing was saved: A team can't be placed under itself or under one of its own teams.", underItself);
        Assert.StartsWith("Nothing was saved: kind must be", kind, StringComparison.Ordinal);
        Assert.StartsWith("Nothing was saved: there is no team with the id 'ghost'.", missing, StringComparison.Ordinal);
        Assert.Equal(new[] { "validate" }, _actions);
    }

    [Fact]
    public async Task DeleteTeam_IsRefusedWithTheReasons_WhileTheTeamHasBlockers()
    {
        // Act
        var refused = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteTeamTool.TheName, Build(), new() { ["teamId"] = "org" }));
        var deleted = AgentToolTestContext.ReadJson(await InvokeAsync(DeleteTeamTool.TheName, Build(), new() { ["teamId"] = "pilot" }));

        // Assert
        Assert.False(refused.GetProperty("deleted").GetBoolean());
        Assert.Contains("still has sub-teams", Assert.Single(refused.GetProperty("reasons").EnumerateArray()).GetString(), StringComparison.Ordinal);
        Assert.True(deleted.GetProperty("deleted").GetBoolean());
        Assert.Equal(new[] { "delete" }, _actions);
        Assert.DoesNotContain(_teams, team => team.ItemId == "pilot");
    }

    private static Dictionary<string, object> Arguments(string toolName)
        => toolName switch
        {
            SaveTeamTool.TheName => new() { ["name"] = "New Team" },
            DeleteTeamTool.TheName => new() { ["teamId"] = "pilot" },
            _ => new(),
        };

    private ServiceProvider Build(bool signedIn = true, Func<Permission, object, bool> authorize = null)
    {
        var services = AgentToolTestContext.CreateServices(signedIn, authorize);
        var manager = new Mock<ITeamManager>();

        manager.Setup(value => value.ListTreeAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => [.. Order(null, 0)]);
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
                    result.Fail(new ValidationResult("The name is required."));
                }

                for (var current = team.ParentId; current is not null; current = _teams.FirstOrDefault(item => item.ItemId == current)?.ParentId)
                {
                    if (current == team.ItemId)
                    {
                        result.Fail(new ValidationResult("A team can't be placed under itself or under one of its own teams."));

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

        services.AddSingleton(manager.Object);

        return services.BuildServiceProvider();
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

    private static Task<string> InvokeAsync(string toolName, IServiceProvider services, Dictionary<string, object> arguments)
    {
        AIFunction tool = toolName switch
        {
            ListTeamsTool.TheName => new ListTeamsTool(),
            SaveTeamTool.TheName => new SaveTeamTool(),
            _ => new DeleteTeamTool(),
        };

        return AgentToolTestContext.InvokeAsync(tool, services, arguments);
    }
}
