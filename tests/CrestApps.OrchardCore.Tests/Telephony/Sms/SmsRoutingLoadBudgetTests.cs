using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Core.Services.Routing;
using CrestApps.OrchardCore.Omnichannel.Sms.Portal.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony.Sms;

/// <summary>
/// Routing runs on the inbound-message path, once per message, and it asks about every candidate agent. What it
/// asks for therefore has to stay cheap as a tenant's history grows: counting an agent's open threads must be a
/// count, not "load everything this agent has ever been assigned and count it here". The strategy read the
/// agent's whole conversation history — years of closed threads — to work out a number the database can produce
/// on its own.
/// </summary>
public sealed class SmsRoutingLoadBudgetTests
{
    [Fact]
    public async Task SelectingAnAgent_CountsOpenThreads_WithoutLoadingThem()
    {
        // Arrange
        var harness = new RoutingHarness();
        harness.WithAgent("agent-1", openThreads: 2);
        harness.WithAgent("agent-2", openThreads: 0);

        // Act
        var selected = await harness.Strategy.SelectAgentAsync("queue-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent-2", selected);
        harness.AssertNoConversationsWereLoaded();
    }

    [Fact]
    public async Task TheNumberOfQueries_DoesNotGrowWithTheSizeOfTheTeam()
    {
        // Arrange
        // A two-hundred-agent queue is the case this is for: asking per agent makes routing one message cost two
        // hundred round trips, which is the difference between a queue that keeps up and one that falls behind.
        var harness = new RoutingHarness();

        for (var i = 0; i < 200; i++)
        {
            harness.WithAgent($"agent-{i:000}", openThreads: i);
        }

        // Act
        var selected = await harness.Strategy.SelectAgentAsync("queue-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent-000", selected);
        harness.AssertLoadWasReadOnceForTheWholeQueue();
    }

    [Fact]
    public async Task TheNumberOfQueries_DoesNotGrowWithATenantsHistory()
    {
        // Arrange
        // The point of the count is that an agent with ten years of closed threads costs the same as a new hire.
        var harness = new RoutingHarness();
        harness.WithAgent("agent-1", openThreads: 1);
        harness.WithAgent("agent-2", openThreads: 5000);

        // Act
        var selected = await harness.Strategy.SelectAgentAsync("queue-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("agent-1", selected);
        harness.AssertNoConversationsWereLoaded();
        harness.AssertLoadWasReadOnceForTheWholeQueue();
    }

    private sealed class RoutingHarness
    {
        private readonly List<AgentProfile> _agents = [];
        private readonly Dictionary<string, int> _openThreads = new(StringComparer.Ordinal);
        private readonly Mock<ISmsConversationStore> _conversationStore = new();
        private readonly Mock<ISmsAgentAvailabilityService> _availability = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();

        public RoutingHarness()
        {
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager.Setup(x => x.GetMembersForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _agents);

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityQueue { ItemId = "queue-1" });

            _availability.Setup(x => x.IsAvailableAsync(It.IsAny<AgentProfile>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);
            _availability.Setup(x => x.Get(It.IsAny<AgentProfile>()))
                .Returns(new SmsAgentAvailability { Available = true, MaxConcurrent = 10_000 });

            _conversationStore.Setup(x => x.CountOpenAssignedAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                    ids.ToDictionary(id => id, id => _openThreads.GetValueOrDefault(id), StringComparer.Ordinal));

            _interactionManager.Setup(x => x.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                    ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal));

            Strategy = new LeastLoadedSmsRoutingStrategy(
                agentManager.Object,
                _conversationStore.Object,
                _availability.Object,
                queueManager.Object,
                _interactionManager.Object,
                new OptionsWrapper<SmsRoutedDistributionOptions>(new SmsRoutedDistributionOptions()));
        }

        public LeastLoadedSmsRoutingStrategy Strategy { get; }

        public void WithAgent(string agentId, int openThreads)
        {
            _agents.Add(new AgentProfile { ItemId = agentId });
            _openThreads[agentId] = openThreads;
        }

        public void AssertNoConversationsWereLoaded()
            => _conversationStore.Verify(
                x => x.GetForAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

        public void AssertLoadWasReadOnceForTheWholeQueue()
        {
            _conversationStore.Verify(
                x => x.CountOpenAssignedAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
                Times.Once);

            // The per-agent count is what the batched read replaced. Reaching for it is the regression.
            _conversationStore.Verify(
                x => x.CountOpenAssignedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);

            _interactionManager.Verify(
                x => x.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
                Times.Once);

            _interactionManager.Verify(
                x => x.CountActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}
