using CrestApps.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Messaging.Core.Services.RoutingRules;
using CrestApps.OrchardCore.Omnichannel.Messaging.Models;
using CrestApps.OrchardCore.Teams.Core.Services;
using CrestApps.OrchardCore.Teams.Models;
using CrestApps.OrchardCore.Teams.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Omnichannel.Messaging;

/// <summary>
/// The routing rules give a conversation nobody owns to an agent: the first enabled rule that applies to it and finds an
/// agent who is available with room for another conversation, picked by the rule's strategy through team assignment.
/// </summary>
public sealed class MessagingRoutingRuleEvaluatorTests
{
    private static readonly DateTime _now = new(2026, 11, 24, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RoundRobin_GivesConversationsToTheSignedInAgentsInTurn()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1", "a2", "a3");
        harness.Ledger.Record("u1", _now.AddMinutes(-5));
        harness.Ledger.Record("u2", _now.AddMinutes(-30));
        harness.Ledger.Record("u3", _now.AddMinutes(-10));
        harness.Rules.Add(Rule("Sales round robin", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales"));

        // Act
        var first = await harness.Evaluator.AssignAsync(Reply(Conversation("c1")), TestContext.Current.CancellationToken);
        var second = await harness.Evaluator.AssignAsync(Reply(Conversation("c2")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("a2", first.AgentId);
        Assert.Equal("a3", second.AgentId);
    }

    [Fact]
    public async Task FewestOpenConversations_GivesItToTheLeastOwnedAgent()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1", "a2", "a3");
        harness.Open["a1"] = 4;
        harness.Open["a2"] = 1;
        harness.Open["a3"] = 2;
        harness.Rules.Add(Rule("Least owned", MessagingRoutingRuleStrategies.LeastOpenConversations, queueId: "sales"));

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(Conversation("c1")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("a2", match.AgentId);
    }

    [Fact]
    public async Task AgentsWhoAreAwayOrFull_AreSkipped_AndARuleWithNobodyFallsToTheNext()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1", "a2");
        harness.SignedIn("overflow", "a9");
        harness.Unavailable.Add("a1");
        harness.Open["a2"] = 10;
        harness.Rules.Add(Rule("Sales", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales", order: 1));
        harness.Rules.Add(Rule("Overflow", MessagingRoutingRuleStrategies.RoundRobin, queueId: "overflow", order: 2));

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(Conversation("c1")), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Overflow", match.Rule.DisplayText);
        Assert.Equal("a9", match.AgentId);
    }

    [Fact]
    public async Task NoRuleFindingAnAgent_LeavesTheConversationAsItWas()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1");
        harness.Unavailable.Add("a1");
        harness.Rules.Add(Rule("Sales", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales"));
        var conversation = Conversation("c1");

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(conversation), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(match);
        Assert.Null(conversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Unassigned, conversation.AssignmentStatus);
        Assert.Empty(conversation.History);
    }

    [Fact]
    public async Task ARuleOnlyTakesTheConversationsItNames()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1");

        var emailOnly = Rule("Email", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        emailOnly.Channels = ["email"];

        var otherNumber = Rule("Other number", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        otherNumber.ServiceAddresses = ["+15550000000"];

        var otherCampaign = Rule("Other campaign", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        otherCampaign.CampaignIds = ["campaign-2"];

        var disabled = Rule("Disabled", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        disabled.Enabled = false;

        var atSendOnly = Rule("At send", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        atSendOnly.Triggers = MessagingRoutingRuleTriggers.StartedWithoutAgent;

        harness.Rules.AddRange([emailOnly, otherNumber, otherCampaign, disabled, atSendOnly]);

        var conversation = Conversation("c1");
        conversation.CampaignId = "campaign-1";

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(conversation), TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(match);
    }

    [Fact]
    public async Task AWaitingRule_WaitsForItsMinutes()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1");
        var rule = Rule("Waiting", MessagingRoutingRuleStrategies.RoundRobin, queueId: "sales");
        rule.Triggers = MessagingRoutingRuleTriggers.Waiting;
        rule.WaitingMinutes = 10;
        harness.Rules.Add(rule);

        var recent = Conversation("c1");
        recent.LastMessageUtc = _now.AddMinutes(-4);
        var old = Conversation("c2");
        old.LastMessageUtc = _now.AddMinutes(-12);

        // Act
        var recentMatch = await harness.Evaluator.AssignAsync(new MessagingRoutingRuleRequest { Conversation = recent, Trigger = MessagingRoutingRuleTriggers.Waiting }, TestContext.Current.CancellationToken);
        var oldMatch = await harness.Evaluator.AssignAsync(new MessagingRoutingRuleRequest { Conversation = old, Trigger = MessagingRoutingRuleTriggers.Waiting }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(recentMatch);
        Assert.Equal("a1", oldMatch.AgentId);
    }

    [Fact]
    public async Task AQueueConversation_StaysTheQueues_AndTheThreadSaysWhichRuleAssignedIt()
    {
        // Arrange
        var harness = new Harness();
        harness.SignedIn("sales", "a1");
        harness.Rules.Add(Rule("Sales", MessagingRoutingRuleStrategies.RoundRobin, queueId: null));
        var queueConversation = Conversation("c1");
        queueConversation.OwnerType = ConversationOwnerType.Queue;
        queueConversation.OwnerId = "sales";
        queueConversation.AssignmentStatus = ConversationAssignmentStatus.Pooled;

        // Act
        await harness.Evaluator.AssignAsync(Reply(queueConversation, queueId: "sales"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ConversationOwnerType.Queue, queueConversation.OwnerType);
        Assert.Equal("sales", queueConversation.OwnerId);
        Assert.Equal("a1", queueConversation.AssignedAgentId);
        Assert.Equal(ConversationAssignmentStatus.Assigned, queueConversation.AssignmentStatus);
        Assert.Equal(_now, queueConversation.AssignedUtc);

        var entry = Assert.Single(queueConversation.History);
        Assert.Equal(MessagingConversationEventKind.Routed, entry.Kind);
        Assert.Equal("Sales", entry.RuleName);
        Assert.Equal("a1", entry.ToAgentId);
        Assert.Equal("Agent a1", entry.ToName);
    }

    [Fact]
    public async Task AConversationOfNoQueue_BecomesTheAgentsOwn()
    {
        // Arrange
        var harness = new Harness();
        harness.WithUserAgent("u5", "a5");
        var rule = Rule("Named", MessagingRoutingRuleStrategies.RoundRobin, queueId: null);
        rule.CandidateSource = MessagingRoutingCandidateSources.Users;
        rule.UserIds = ["u5", "no-agent-profile"];
        harness.Rules.Add(rule);
        var conversation = Conversation("c1");

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(conversation), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("a5", match.AgentId);
        Assert.Equal(ConversationOwnerType.Personal, conversation.OwnerType);
        Assert.Equal("a5", conversation.OwnerId);
    }

    [Fact]
    public async Task TeamsAndRoles_AreReadForTheirUsers()
    {
        // Arrange
        var harness = new Harness();
        harness.WithUserAgent("u6", "a6");
        harness.WithUserAgent("u7", "a7");
        harness.TeamMembers["team-1"] = ["u6"];
        harness.RoleUsers["Closers"] = ["u7"];

        var teamRule = Rule("Team", MessagingRoutingRuleStrategies.RoundRobin, queueId: null, order: 1);
        teamRule.CandidateSource = MessagingRoutingCandidateSources.Teams;
        teamRule.TeamIds = ["team-1"];

        var roleRule = Rule("Role", MessagingRoutingRuleStrategies.RoundRobin, queueId: null, order: 2);
        roleRule.CandidateSource = MessagingRoutingCandidateSources.Roles;
        roleRule.RoleNames = ["Closers"];
        roleRule.Channels = ["whatsapp"];

        harness.Rules.AddRange([teamRule, roleRule]);

        var whatsApp = Conversation("c2");
        whatsApp.Channel = "whatsapp";
        harness.Unavailable.Add("a6");

        // Act
        var match = await harness.Evaluator.AssignAsync(Reply(whatsApp), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("Role", match.Rule.DisplayText);
        Assert.Equal("a7", match.AgentId);
    }

    private static MessagingRoutingRule Rule(string name, string strategy, string queueId, int order = 0)
        => new()
        {
            ItemId = name,
            DisplayText = name,
            Order = order,
            Strategy = strategy,
            CandidateSource = MessagingRoutingCandidateSources.QueueSignedIn,
            CandidateQueueId = queueId,
        };

    private static MessagingConversation Conversation(string id)
        => new()
        {
            ItemId = id,
            Channel = "sms",
            ServiceAddress = "+15551110000",
            ContactAddress = "+15552220000",
            AssignmentStatus = ConversationAssignmentStatus.Unassigned,
        };

    private static MessagingRoutingRuleRequest Reply(MessagingConversation conversation, string queueId = null)
        => new()
        {
            Conversation = conversation,
            Trigger = MessagingRoutingRuleTriggers.ContactReplied,
            QueueId = queueId,
        };

    private sealed class Harness
    {
        private readonly Dictionary<string, List<AgentProfile>> _signedIn = new(StringComparer.Ordinal);
        private readonly Dictionary<string, AgentProfile> _byUserId = new(StringComparer.Ordinal);

        public Harness()
        {
            var rules = new Mock<ICatalog<MessagingRoutingRule>>();
            rules.Setup(catalog => catalog.GetAllAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => Rules.ToArray());

            var agents = new Mock<IAgentProfileManager>();
            agents.Setup(manager => manager.GetSignedInForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _signedIn.TryGetValue(queueId, out var list) ? list.ToArray() : []);
            agents.Setup(manager => manager.FindByUserIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string userId, CancellationToken _) => _byUserId.GetValueOrDefault(userId));

            var queues = new Mock<IActivityQueueManager>();

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
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            var store = new Mock<IMessagingConversationStore>();
            store.Setup(value => value.CountOpenAssignedAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                    ids.ToDictionary(id => id, id => Open.GetValueOrDefault(id), StringComparer.Ordinal));

            var availability = new Mock<IMessagingAvailabilityService>();
            availability.Setup(value => value.Get(It.IsAny<AgentProfile>())).Returns(new MessagingAgentAvailability { MaxConcurrent = 10 });
            availability.Setup(value => value.IsAvailableAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((AgentProfile agent, CancellationToken _) => !Unavailable.Contains(agent.ItemId));

            var interactions = new Mock<IInteractionManager>();
            interactions.Setup(value => value.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) => ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal));

            var names = new Mock<IMessagingAgentNameProvider>();
            names.Setup(value => value.GetDisplayNameAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string agentId, CancellationToken _) => $"Agent {agentId}");

            var options = Options.Create(new MessagingRoutedDistributionOptions());
            var snapshot = new MessagingRoutingLoadSnapshot();

            var assignment = new DefaultTeamAssignmentService(
                memberships.Object,
                [
                    new MessagingConversationLoadSource(snapshot, agents.Object, store.Object, availability.Object, interactions.Object, options, Ledger),
                    new MessagingWeightedConversationLoadSource(snapshot, agents.Object, store.Object, availability.Object, interactions.Object, options, Ledger),
                ],
                []);

            Evaluator = new MessagingRoutingRuleEvaluator(
                rules.Object,
                new MessagingRoutingCandidateResolver(agents.Object, queues.Object, memberships.Object, users.Object, clock.Object),
                store.Object,
                availability.Object,
                interactions.Object,
                options,
                snapshot,
                assignment,
                Ledger,
                names.Object,
                clock.Object,
                NullLogger<MessagingRoutingRuleEvaluator>.Instance);
        }

        public List<MessagingRoutingRule> Rules { get; } = [];

        public Dictionary<string, int> Open { get; } = new(StringComparer.Ordinal);

        public HashSet<string> Unavailable { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string[]> TeamMembers { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, string[]> RoleUsers { get; } = new(StringComparer.Ordinal);

        public InMemoryLedger Ledger { get; } = new();

        public MessagingRoutingRuleEvaluator Evaluator { get; }

        public void SignedIn(string queueId, params string[] agentIds)
        {
            var list = _signedIn.TryGetValue(queueId, out var existing) ? existing : _signedIn[queueId] = [];

            foreach (var agentId in agentIds)
            {
                var agent = new AgentProfile { ItemId = agentId, UserId = "u" + agentId[1..] };
                list.Add(agent);
                _byUserId[agent.UserId] = agent;
            }
        }

        public void WithUserAgent(string userId, string agentId)
            => _byUserId[userId] = new AgentProfile { ItemId = agentId, UserId = userId };
    }

    private sealed class InMemoryLedger : IMessagingRoutingLedger
    {
        private readonly Dictionary<string, DateTime> _lastAssigned = new(StringComparer.Ordinal);

        public void Record(string userId, DateTime assignedUtc)
            => _lastAssigned[userId] = assignedUtc;

        public Task<IReadOnlyDictionary<string, DateTime>> GetLastAssignedAsync(IReadOnlyCollection<string> userIds, CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyDictionary<string, DateTime>>(userIds
                .Where(_lastAssigned.ContainsKey)
                .ToDictionary(id => id, id => _lastAssigned[id], StringComparer.Ordinal));

        public Task RecordAsync(string userId, DateTime assignedUtc, CancellationToken cancellationToken = default)
        {
            Record(userId, assignedUtc);

            return Task.CompletedTask;
        }
    }

    private sealed class TestUser : IUser
    {
        public string UserId { get; set; }

        public string UserName { get; set; }
    }
}
