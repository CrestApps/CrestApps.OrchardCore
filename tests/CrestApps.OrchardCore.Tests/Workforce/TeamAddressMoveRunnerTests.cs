using System.Text.Json.Nodes;
using CrestApps.Core.Models;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Workforce;

/// <summary>
/// The move of a team's one-line address to its own address runs until it has finished once: a move that fails halfway
/// finishes at the next start, and a team saved in between keeps its old line.
/// </summary>
public sealed class TeamAddressMoveRunnerTests
{
    [Fact]
    public async Task AMoveThatFailsHalfway_FinishesAtTheNextStart_AndLosesNoAddress()
    {
        // Arrange
        await using var harness = await WorkforceTests.CreateAsync();
        var lines = new Dictionary<string, string> { ["Boston"] = "1 Main St", ["Denver"] = "5 Pine Ave", ["Austin"] = "9 Oak Rd" };
        var ids = new List<string>();

        foreach (var (name, line) in lines)
        {
            var team = await WorkforceTests.AddTeamAsync(harness, name, TeamKind.Site);

            await PlanningKit.UpdateTeamAsync(harness, team, saved => saved.Properties[nameof(WorkforceTeamSettings)] = new JsonObject
            {
                ["Location"] = new JsonObject { ["Address"] = line, ["Latitude"] = 40.0, ["Longitude"] = -100.0 },
            });
            ids.Add(team.ItemId);
        }

        // Act: the first start fails on the second team it saves.
        var first = await harness.RunAsync(async services =>
        {
            var real = services.GetRequiredService<ITeamManager>();
            var updates = 0;
            var failing = new Mock<ITeamManager>();

            failing.Setup(manager => manager.GetAllAsync(It.IsAny<CancellationToken>())).Returns((CancellationToken token) => real.GetAllAsync(token));
            failing
                .Setup(manager => manager.UpdateAsync(It.IsAny<Team>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns((Team team, JsonNode data, CancellationToken token) => ++updates == 2
                    ? throw new InvalidOperationException("The database went away.")
                    : real.UpdateAsync(team, data, token));

            return await new WorkforceTeamAddressMoveRunner(services.GetRequiredService<ISession>(), failing.Object, NullLogger<WorkforceTeamAddressMoveRunner>.Instance).RunAsync();
        });

        // A team is saved in between, as an edit of its Workforce settings: its old line stays for the move.
        await PlanningKit.UpdateTeamAsync(harness, await FindAsync(harness, ids[2]), saved => saved.AlterWorkforceSettings(settings => settings.TimeClockPolicyId = "policy"));

        var second = await harness.RunAsync(services => ActivatorUtilities.CreateInstance<WorkforceTeamAddressMoveRunner>(services).RunAsync());
        var third = await harness.RunAsync(services => ActivatorUtilities.CreateInstance<WorkforceTeamAddressMoveRunner>(services).RunAsync());
        var teams = new List<Team>();

        foreach (var id in ids)
        {
            teams.Add(await FindAsync(harness, id));
        }

        // Assert
        Assert.False(first);
        Assert.True(second);
        Assert.True(third);
        Assert.Equal(lines.Values, teams.Select(team => team.Address?.AddressLine1));
        Assert.All(teams, team => Assert.Equal(-100.0, team.GetWorkforceSettings().Location.Longitude));
        Assert.All(teams, team => Assert.DoesNotContain("\"Address\"", System.Text.Json.JsonSerializer.Serialize(team.Properties[nameof(WorkforceTeamSettings)]), StringComparison.Ordinal));
        Assert.Equal("policy", teams[2].GetWorkforceSettings().TimeClockPolicyId);
    }

    private static Task<Team> FindAsync(Calendars.CalendarTestHarness harness, string id)
        => harness.RunAsync(services => services.GetRequiredService<ITeamManager>().FindByIdAsync(id).AsTask(), commit: false);
}
