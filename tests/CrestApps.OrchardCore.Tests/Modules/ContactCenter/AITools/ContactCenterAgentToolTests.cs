using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.AI;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Agent;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.AITools;

/// <summary>
/// The agent tools read agent profiles as the Agent entitlements page does, change them through the presence manager
/// with every setting not given passed through, and refuse queues and campaigns that do not exist.
/// </summary>
public sealed class ContactCenterAgentToolTests
{
    private readonly ContactCenterSetupToolHarness _harness = new();
    private AgentManagedConfiguration _applied;

    public ContactCenterAgentToolTests()
    {
        _harness.AddQueue("sales", "Sales");
        _harness.AddQueue("renewals", "Renewals");
        _harness.Presence.Setup(manager => manager.ApplyManagedConfigurationAsync(It.IsAny<string>(), It.IsAny<AgentManagedConfiguration>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string agentId, AgentManagedConfiguration configuration, CancellationToken _) =>
            {
                _applied = configuration;

                return _harness.Agents.Single(agent => agent.ItemId == agentId);
            });
    }

    [Fact]
    public async Task ListAgents_FindsTheAgentsAllowedInAQueue()
    {
        // Arrange
        _harness.AddAgent("agent-1", "maria", agent => agent.AllowedQueueIds = ["sales"]);
        _harness.AddAgent("agent-2", "omar", agent => agent.AllowedQueueIds = ["renewals"]);

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListContactCenterAgentsTool(), _harness.Build(), new() { ["queueId"] = "sales" }));

        // Assert
        var agent = Assert.Single(json.GetProperty("items").EnumerateArray());
        Assert.Equal("maria", agent.GetProperty("userName").GetString());
        Assert.Equal("Sales", agent.GetProperty("allowedQueues")[0].GetProperty("name").GetString());
        Assert.True(json.GetProperty("entitlementsEnforced").GetBoolean());
    }

    [Fact]
    public async Task ListAgents_SaysAllowListsAreNotEnforcedWithoutEntitlements()
    {
        // Arrange
        _harness.Features.Remove(ContactCenterConstants.Feature.AgentEntitlements);
        _harness.AddAgent("agent-1", "maria");

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new ListContactCenterAgentsTool(), _harness.Build()));

        // Assert
        Assert.False(json.GetProperty("entitlementsEnforced").GetBoolean());
        Assert.Contains("not enforced", json.GetProperty("note").GetString());
    }

    [Fact]
    public async Task SaveAgent_ChangingOnlyTheConcurrencyKeepsSkillsQueuesAndMemberships()
    {
        // Arrange
        _harness.AddAgent("agent-1", "maria", agent =>
        {
            agent.AllowedQueueIds = ["sales"];
            agent.Skills = ["spanish", "billing"];
            agent.SkillProficiencies = [new AgentSkill { SkillId = "spanish", Proficiency = 4 }];
            agent.QueueMemberships = [new AgentQueueMembership { QueueId = "sales", Priority = 2 }];
        });

        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new()
        {
            ["agentId"] = "agent-1",
            ["maxConcurrentInteractions"] = 3,
        }));

        // Assert
        Assert.Equal("updated", json.GetProperty("saved").GetString());
        Assert.Equal(3, _applied.MaxConcurrentInteractions);
        Assert.Equal(["sales"], _applied.AllowedQueueIds);
        Assert.Equal(["spanish", "billing"], _applied.Skills);
        Assert.Equal(4, Assert.Single(_applied.SkillProficiencies).Proficiency);
        Assert.Equal(2, Assert.Single(_applied.QueueMemberships).Priority);
        Assert.Equal("maria", _applied.DisplayName);
    }

    [Fact]
    public async Task SaveAgent_GivesQueuesAndASkillLevel()
    {
        // Arrange
        _harness.AddAgent("agent-1", "maria", agent => agent.Skills = ["billing"]);

        // Act
        await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new()
        {
            ["userName"] = "maria",
            ["allowedQueueIds"] = new[] { "sales", "renewals" },
            ["skills"] = new[] { new Dictionary<string, object> { ["skillId"] = "Spanish", ["proficiency"] = 4 } },
        });

        // Assert
        Assert.Equal(["sales", "renewals"], _applied.AllowedQueueIds);
        Assert.Null(_applied.Skills);
        var skill = Assert.Single(_applied.SkillProficiencies);
        Assert.Equal(("Spanish", 4), (skill.SkillId, skill.Proficiency));
    }

    [Fact]
    public async Task SaveAgent_RefusesAQueueThatDoesNotExist()
    {
        // Arrange
        _harness.AddAgent("agent-1", "maria");

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new()
        {
            ["agentId"] = "agent-1",
            ["allowedQueueIds"] = new[] { "sales", "missing" },
        });

        // Assert
        Assert.Equal("Nothing was saved: No queue has the ID 'missing'.", result);
        Assert.Empty(_harness.Presence.Invocations);
    }

    [Fact]
    public async Task SaveAgent_RefusesAMembershipOfAQueueTheAgentIsNotAllowedIn()
    {
        // Arrange
        _harness.AddAgent("agent-1", "maria", agent => agent.AllowedQueueIds = ["sales"]);

        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new()
        {
            ["agentId"] = "agent-1",
            ["queueMemberships"] = new[] { new Dictionary<string, object> { ["queueId"] = "renewals", ["priority"] = 1 } },
        });

        // Assert
        Assert.StartsWith("Nothing was saved: Queue 'renewals' is not among the agent's allowed queues", result);
        Assert.Empty(_harness.Presence.Invocations);
    }

    [Fact]
    public async Task SaveAgent_CreatesTheProfileOfAUserWithoutOne()
    {
        // Act
        var json = AgentToolTestContext.ReadJson(await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new()
        {
            ["userName"] = "omar",
            ["allowedQueueIds"] = new[] { "sales" },
        }));

        // Assert
        Assert.Equal("created", json.GetProperty("saved").GetString());
        var agent = Assert.Single(_harness.CreatedAgents);
        Assert.Equal(("user-omar", "omar", "omar", 1), (agent.UserId, agent.UserName, agent.DisplayName, agent.MaxConcurrentInteractions));
        Assert.Equal(["sales"], agent.AllowedQueueIds);
        Assert.Equal(ContactCenterSetupToolHarness.Now, agent.CreatedUtc);
    }

    [Fact]
    public async Task SaveAgent_SaysSoWhenTheUserDoesNotExist()
    {
        // Act
        var result = await AgentToolTestContext.InvokeAsync(new SaveContactCenterAgentTool(), _harness.Build(), new() { ["userName"] = "nobody" });

        // Assert
        Assert.Equal("Nothing was saved: no user is named 'nobody'.", result);
    }
}
