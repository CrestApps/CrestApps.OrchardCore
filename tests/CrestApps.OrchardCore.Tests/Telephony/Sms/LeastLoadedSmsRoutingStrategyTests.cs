using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Models;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services;
using CrestApps.OrchardCore.Sms.Workspace.Core.Services.Routing;
using CrestApps.OrchardCore.Sms.Workspace.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

public class LeastLoadedSmsRoutingStrategyTests
{
    [Fact]
    public async Task Selects_TheLeastLoadedAvailableMember()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "q1", available: true, load: 3);
        harness.AddAgent("a2", "q1", available: true, load: 1);
        harness.AddAgent("a3", "q1", available: true, load: 2);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task Skips_UnavailableAgents()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "q1", available: false, load: 0);
        harness.AddAgent("a2", "q1", available: true, load: 5);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task Skips_AgentsAtOrOverTheirConcurrencyCap()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "q1", available: true, load: 2, maxConcurrent: 2);
        harness.AddAgent("a2", "q1", available: true, load: 4, maxConcurrent: 10);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task Skips_NonMembersOfTheQueue()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "other-queue", available: true, load: 0);
        harness.AddAgent("a2", "q1", available: true, load: 3);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task Excludes_TheExcludedAgent()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "q1", available: true, load: 0);
        harness.AddAgent("a2", "q1", available: true, load: 4);

        var selected = await harness.Strategy.SelectAgentAsync("q1", excludeAgentId: "a1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task Returns_Null_WhenNoEligibleAgent()
    {
        var harness = new Harness();
        harness.AddAgent("a1", "q1", available: false, load: 0);
        harness.AddAgent("a2", "q1", available: true, load: 10, maxConcurrent: 10);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Null(selected);
    }

    [Fact]
    public async Task Skips_AgentsMissingRequiredQueueSkills()
    {
        var harness = new Harness();
        harness.SetQueueRequiredSkills("q1", "spanish");
        harness.AddAgent("a1", "q1", available: true, load: 0, skills: ["english"]);
        harness.AddAgent("a2", "q1", available: true, load: 4, skills: ["spanish", "english"]);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    [Fact]
    public async Task CapacityWeighting_CountsLiveVoiceWorkAgainstTheSmsBudget()
    {
        var harness = new Harness();
        // a1 idle on SMS but on a live call (weight 3) → effective load 3; a2 has 2 SMS, no calls → load 2 wins.
        harness.AddAgent("a1", "q1", available: true, load: 0, maxConcurrent: 10, activeVoice: 1);
        harness.AddAgent("a2", "q1", available: true, load: 2, maxConcurrent: 10, activeVoice: 0);

        var selected = await harness.Strategy.SelectAgentAsync("q1", cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal("a2", selected);
    }

    private sealed class Harness
    {
        private readonly List<AgentProfile> _agents = [];
        private readonly Dictionary<string, ActivityQueue> _queues = new(StringComparer.Ordinal);
        private readonly Mock<IAgentProfileManager> _agentManager = new();
        private readonly Mock<ISmsConversationStore> _conversationStore = new();
        private readonly Mock<ISmsAgentAvailabilityService> _availability = new();
        private readonly Mock<IActivityQueueManager> _queueManager = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();

        public Harness()
        {
            // The manager's indexed lookup returns only the queue's members; the strategy no longer filters
            // membership itself.
            _agentManager.Setup(m => m.GetMembersForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _agents
                    .Where(a => a.AllowedQueueIds.Contains(queueId, StringComparer.Ordinal))
                    .ToArray());

            _queueManager.Setup(m => m.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _queues.GetValueOrDefault(queueId) ?? new ActivityQueue { ItemId = queueId });

            _interactionManager.Setup(m => m.CountActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(0);

            Strategy = new LeastLoadedSmsRoutingStrategy(
                _agentManager.Object,
                _conversationStore.Object,
                _availability.Object,
                _queueManager.Object,
                _interactionManager.Object,
                Microsoft.Extensions.Options.Options.Create(new SmsRoutedDistributionOptions()));
        }

        public LeastLoadedSmsRoutingStrategy Strategy { get; }

        public void SetQueueRequiredSkills(string queueId, params string[] skills)
        {
            _queues[queueId] = new ActivityQueue { ItemId = queueId, RequiredSkills = [.. skills] };
        }

        public void AddAgent(string agentId, string queueId, bool available, int load, int maxConcurrent = 10, string[] skills = null, int activeVoice = 0)
        {
            var agent = new AgentProfile { ItemId = agentId, UserId = "u-" + agentId };
            agent.AllowedQueueIds.Add(queueId);

            if (skills is not null)
            {
                foreach (var skill in skills)
                {
                    agent.Skills.Add(skill);
                }
            }

            _agents.Add(agent);

            _availability.Setup(a => a.Get(It.Is<AgentProfile>(x => x.ItemId == agentId)))
                .Returns(new SmsAgentAvailability { Available = available, MaxConcurrent = maxConcurrent });

            var conversations = Enumerable.Range(0, load)
                .Select(_ => new SmsConversation
                {
                    Status = SmsConversationStatus.Open,
                    AssignmentStatus = SmsConversationAssignmentStatus.Assigned,
                })
                .ToArray();

            _conversationStore.Setup(s => s.GetForAgentAsync(agentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(conversations);

            _interactionManager.Setup(m => m.CountActiveByAgentAsync(agentId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(activeVoice);
        }
    }
}
