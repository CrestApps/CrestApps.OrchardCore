using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// A routing rule's "who" is read from its source: the agents signed in to a queue or working it, the members of
/// teams, the users in roles, or a list of users. Only users with an agent profile can get a conversation.
/// </summary>
public sealed class MessagingRoutingCandidateResolverTests
{
    [Fact]
    public async Task QueueSignedIn_ReadsTheConversationsQueue_WhenTheRuleNamesNone()
    {
        var harness = new Harness();
        harness.SignedIn["sales"] = [Agent("a1", "u1"), Agent("a2", "u2")];

        var candidates = await harness.Resolver.ResolveAsync(Rule(MessagingRoutingCandidateSources.QueueSignedIn), "sales", TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a2"], candidates.Agents.Select(agent => agent.ItemId));
    }

    [Fact]
    public async Task QueueSignedIn_ReadsTheRulesOwnQueue_OverTheConversations()
    {
        var harness = new Harness();
        harness.SignedIn["sales"] = [Agent("a1", "u1")];
        harness.SignedIn["support"] = [Agent("a9", "u9")];

        var rule = Rule(MessagingRoutingCandidateSources.QueueSignedIn);
        rule.CandidateQueueId = "support";

        var candidates = await harness.Resolver.ResolveAsync(rule, "sales", TestContext.Current.CancellationToken);

        Assert.Equal(["a9"], candidates.Agents.Select(agent => agent.ItemId));
    }

    [Fact]
    public async Task AQueueSource_WithNoQueueAtAll_HasNobody()
    {
        var harness = new Harness();

        var candidates = await harness.Resolver.ResolveAsync(Rule(MessagingRoutingCandidateSources.QueueSignedIn), conversationQueueId: null, TestContext.Current.CancellationToken);

        Assert.Empty(candidates.Agents);
    }

    [Fact]
    public async Task QueueMembers_ReadsEveryAgentWhoWorksTheQueue_AndItsRequiredSkills()
    {
        var harness = new Harness();
        harness.Members["sales"] = [Agent("a1", "u1"), Agent("a3", "u3")];
        harness.Queues["sales"] = new ActivityQueue { ItemId = "sales", RequiredSkills = ["Spanish"] };

        var candidates = await harness.Resolver.ResolveAsync(Rule(MessagingRoutingCandidateSources.QueueMembers), "sales", TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a3"], candidates.Agents.Select(agent => agent.ItemId));
        Assert.Single(candidates.RequiredSkills);
    }

    [Fact]
    public async Task Teams_ReadsEveryTeamsMembers_Once()
    {
        var harness = new Harness();
        harness.TeamMembers["team-1"] = ["u1", "u2"];
        harness.TeamMembers["team-2"] = ["u2", "u3"];
        harness.WithAgents(("u1", "a1"), ("u2", "a2"), ("u3", "a3"));

        var rule = Rule(MessagingRoutingCandidateSources.Teams);
        rule.TeamIds = ["team-1", "team-2"];

        var candidates = await harness.Resolver.ResolveAsync(rule, null, TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a2", "a3"], candidates.Agents.Select(agent => agent.ItemId));
    }

    [Fact]
    public async Task Roles_ReadTheirUsers_AndSkipUsersWithoutAnAgentProfile()
    {
        var harness = new Harness();
        harness.RoleUsers["Sales"] = ["u1", "u-no-agent"];
        harness.WithAgents(("u1", "a1"));

        var rule = Rule(MessagingRoutingCandidateSources.Roles);
        rule.RoleNames = ["Sales"];

        var candidates = await harness.Resolver.ResolveAsync(rule, null, TestContext.Current.CancellationToken);

        Assert.Equal(["a1"], candidates.Agents.Select(agent => agent.ItemId));
    }

    [Fact]
    public async Task Users_AreReadOnce_IgnoringBlanks()
    {
        var harness = new Harness();
        harness.WithAgents(("u1", "a1"), ("u2", "a2"));

        var rule = Rule(MessagingRoutingCandidateSources.Users);
        rule.UserIds = ["u1", " ", "u2", "u1"];

        var candidates = await harness.Resolver.ResolveAsync(rule, null, TestContext.Current.CancellationToken);

        Assert.Equal(["a1", "a2"], candidates.Agents.Select(agent => agent.ItemId));
    }

    [Fact]
    public async Task AnUnknownSource_HasNobody()
    {
        var harness = new Harness();

        var candidates = await harness.Resolver.ResolveAsync(Rule("Somebody"), "sales", TestContext.Current.CancellationToken);

        Assert.Empty(candidates.Agents);
    }

    [Fact]
    public async Task TheRoundRobinLedger_RemembersWhenEachUserLastGotAConversation_PerTenant()
    {
        var cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        var tenantA = new DistributedCacheMessagingRoutingLedger(cache, new ShellSettings { Name = "A" });
        var tenantB = new DistributedCacheMessagingRoutingLedger(cache, new ShellSettings { Name = "B" });
        var assignedUtc = new DateTime(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

        await tenantA.RecordAsync("u1", assignedUtc, TestContext.Current.CancellationToken);

        var a = await tenantA.GetLastAssignedAsync(["u1", "u2"], TestContext.Current.CancellationToken);
        var b = await tenantB.GetLastAssignedAsync(["u1"], TestContext.Current.CancellationToken);

        Assert.Equal(assignedUtc, a["u1"]);
        Assert.Equal(DateTimeKind.Utc, a["u1"].Kind);
        Assert.False(a.ContainsKey("u2"));
        Assert.Empty(b);
    }

    private static MessagingRoutingRule Rule(string source)
        => new()
        {
            ItemId = "rule-1",
            DisplayText = "Rule",
            Enabled = true,
            CandidateSource = source,
        };

    private static AgentProfile Agent(string agentId, string userId)
        => new() { ItemId = agentId, UserId = userId };

    private sealed class Harness
    {
        private readonly Dictionary<string, AgentProfile> _byUserId = new(StringComparer.Ordinal);

        public Harness()
        {
            var agents = new Mock<IAgentProfileManager>();
            agents.Setup(manager => manager.GetSignedInForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => SignedIn.TryGetValue(queueId, out var list) ? list : []);
            agents.Setup(manager => manager.GetMembersForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => Members.TryGetValue(queueId, out var list) ? list : []);
            agents.Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, CancellationToken _) => _byUserId.GetValueOrDefault(userId));

            var queues = new Mock<IActivityQueueManager>();
            queues.Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => Queues.GetValueOrDefault(queueId));

            var memberships = new Mock<ITeamMembershipService>();
            memberships.Setup(service => service.GetMembersAsync(It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<DateOnly?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string teamId, bool _, DateOnly? _, CancellationToken _) =>
                    TeamMembers.TryGetValue(teamId, out var users)
                        ? users.Select(userId => new TeamMember { TeamId = teamId, UserId = userId }).ToList()
                        : []);

            var users = new Mock<UserManager<IUser>>(new Mock<IUserStore<IUser>>().Object, null, null, null, null, null, null, null, null);
            users.Setup(manager => manager.GetUsersInRoleAsync(It.IsAny<string>()))
                .ReturnsAsync((string role) => RoleUsers.TryGetValue(role, out var ids)
                    ? ids.Select(id => (IUser)new TestUser { UserId = id }).ToList()
                    : []);
            users.Setup(manager => manager.GetUserIdAsync(It.IsAny<IUser>()))
                .ReturnsAsync((IUser user) => ((TestUser)user).UserId);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc));

            Resolver = new MessagingRoutingCandidateResolver(agents.Object, queues.Object, memberships.Object, users.Object, clock.Object);
        }

        public Dictionary<string, AgentProfile[]> SignedIn { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, AgentProfile[]> Members { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, ActivityQueue> Queues { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string[]> TeamMembers { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string[]> RoleUsers { get; } = new(StringComparer.Ordinal);

        public MessagingRoutingCandidateResolver Resolver { get; }

        public void WithAgents(params (string UserId, string AgentId)[] agents)
        {
            foreach (var (userId, agentId) in agents)
            {
                _byUserId[userId] = Agent(agentId, userId);
            }
        }
    }

    private sealed class TestUser : IUser
    {
        public string UserId { get; set; }

        public string UserName { get; set; }
    }
}
