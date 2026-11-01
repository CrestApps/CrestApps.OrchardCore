using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Workforce.ContactCenter.Services;
using CrestApps.OrchardCore.Workforce.Models;
using CrestApps.OrchardCore.Workforce.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace CrestApps.OrchardCore.Tests.Workforce.ContactCenter;

/// <summary>
/// Agents and workers are linked through their user: an agent gets a worker once, and agents without a user are left
/// out.
/// </summary>
public sealed class AgentWorkerServiceTests
{
    [Fact]
    public async Task EveryAgentWithAUser_GetsOneWorker()
    {
        var agents = new[]
        {
            new AgentProfile { ItemId = "a1", UserId = "user-ana", DisplayName = "Ana Agent" },
            new AgentProfile { ItemId = "a2", UserId = "user-ben", UserName = "ben" },
            new AgentProfile { ItemId = "a3", Name = "No user" },
        };
        var manager = new Mock<IAgentProfileManager>();

        manager.Setup(m => m.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(agents);

        await using var harness = await WorkforceTests.CreateAsync(services =>
        {
            services.AddSingleton(manager.Object);
            services.AddScoped<AgentWorkerService>();
        });

        var team = await WorkforceTests.AddTeamAsync(harness, "Support", TeamKind.Team);
        var existing = await PlanningKit.AddMemberAsync(harness, "Ana", team);

        var created = await harness.RunAsync(services => services.GetRequiredService<AgentWorkerService>().CreateMissingAsync(team.ItemId));
        var again = await harness.RunAsync(services => services.GetRequiredService<AgentWorkerService>().CreateMissingAsync(team.ItemId));

        Assert.Equal(1, created);
        Assert.Equal(0, again);

        var list = await harness.RunAsync(services => services.GetRequiredService<AgentWorkerService>().ListAsync(), commit: false);

        Assert.Equal(2, list.Count);
        Assert.Equal(existing.ItemId, list.Single(entry => entry.Agent.ItemId == "a1").Worker.ItemId);

        var ben = list.Single(entry => entry.Agent.ItemId == "a2").Worker;
        Assert.Equal("ben", ben.DisplayName);
        Assert.Equal(team.ItemId, ben.PrimaryTeamId);

        var none = await harness.RunAsync(services => services.GetRequiredService<AgentWorkerService>().EnsureWorkerAsync(agents[2]), commit: false);
        Assert.Null(none.Worker);
    }
}
