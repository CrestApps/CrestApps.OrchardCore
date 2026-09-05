using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An agent signed into Sales and Support was always served from whichever queue happened to be first in their
/// stored list, so a caller who had been waiting twenty minutes on the second queue sat behind one who had just
/// arrived on the first. The selector picks the queue holding the contact who most deserves to be answered, which
/// is what every production ACD does and what SLA aging is meaningless without.
/// </summary>
public sealed class AgentWorkSelectorTests
{
    private static readonly DateTime _now = new(2026, 3, 4, 15, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Selects_TheQueueHoldingTheOldestCaller_NotTheFirstQueueInTheList()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("sales", waitingSince: _now.AddMinutes(-1));
        harness.AddQueue("support", waitingSince: _now.AddMinutes(-20));

        var agent = Agent(("sales", 0, 0), ("support", 0, 0));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("support", queueId);
    }

    [Fact]
    public async Task MembershipPriority_OutranksAge()
    {
        // Arrange
        // A supervisor who marks a queue as this agent's primary is saying it comes first even when another
        // queue has been waiting longer; that is the whole point of the setting.
        var harness = new Harness();
        harness.AddQueue("sales", waitingSince: _now.AddMinutes(-1));
        harness.AddQueue("support", waitingSince: _now.AddMinutes(-20));

        var agent = Agent(("sales", 0, 0), ("support", 5, 0));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sales", queueId);
    }

    [Fact]
    public async Task Delay_KeepsAQueueOutOfReach_UntilTheItemHasWaitedLongEnough()
    {
        // Arrange
        // A delay is how an overflow queue is expressed: this agent is the backup, so do not pull from here
        // until the specialists have had their chance.
        var harness = new Harness();
        harness.AddQueue("overflow", waitingSince: _now.AddSeconds(-30));

        var agent = Agent(("overflow", 0, 60));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(queueId);
    }

    [Fact]
    public async Task Delay_ReleasesTheQueue_OnceTheItemHasWaitedLongEnough()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("overflow", waitingSince: _now.AddSeconds(-90));

        var agent = Agent(("overflow", 0, 60));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("overflow", queueId);
    }

    [Fact]
    public async Task SlaAging_LetsALongWaitingNormalCall_BeatANewerHighPriorityOne()
    {
        // Arrange
        // Aging exists so a normal caller is not starved behind a stream of priority ones. It only means
        // anything if the selector reads the aged priority rather than the stored one.
        var harness = new Harness();
        harness.AddQueue("aged", waitingSince: _now.AddMinutes(-30), priority: InteractionPriority.Normal, slaThresholdSeconds: 60, enableSlaAging: true);
        harness.AddQueue("urgent", waitingSince: _now.AddSeconds(-5), priority: InteractionPriority.High);

        var agent = Agent(("aged", 0, 0), ("urgent", 0, 0));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("aged", queueId);
    }

    [Fact]
    public async Task Skips_QueuesWithNobodyWaiting()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("empty", waitingSince: null);
        harness.AddQueue("busy", waitingSince: _now.AddMinutes(-2));

        var agent = Agent(("empty", 0, 0), ("busy", 0, 0));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("busy", queueId);
    }

    [Fact]
    public async Task Selects_Nothing_WhenNoQueueHasAnybodyWaiting()
    {
        // Arrange
        var harness = new Harness();
        harness.AddQueue("empty", waitingSince: null);

        var agent = Agent(("empty", 0, 0));

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(queueId);
    }

    [Fact]
    public async Task Reads_LegacyQueueIds_WhenNoMembershipsAreStored()
    {
        // Arrange
        // Existing agents were signed in before memberships existed. Ignoring their stored list would silently
        // sign them out of every queue on upgrade.
        var harness = new Harness();
        harness.AddQueue("sales", waitingSince: _now.AddMinutes(-3));

        var agent = new AgentProfile { ItemId = "a1", QueueIds = ["sales"] };

        // Act
        var queueId = await harness.Selector.SelectNextForAgentAsync(agent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("sales", queueId);
    }

    private static AgentProfile Agent(params (string QueueId, int Priority, int DelaySeconds)[] memberships)
    {
        var agent = new AgentProfile { ItemId = "a1" };

        foreach (var (queueId, priority, delaySeconds) in memberships)
        {
            agent.QueueIds.Add(queueId);
            agent.QueueMemberships.Add(new AgentQueueMembership
            {
                QueueId = queueId,
                Priority = priority,
                DelaySeconds = delaySeconds,
            });
        }

        return agent;
    }

    private sealed class Harness
    {
        private readonly Dictionary<string, ActivityQueue> _queues = new(StringComparer.Ordinal);
        private readonly Dictionary<string, QueueItem> _heads = new(StringComparer.Ordinal);

        public Harness()
        {
            var queueItemStore = new Mock<IQueueItemStore>();
            queueItemStore
                .Setup(store => store.GetHeadWaitingByQueueAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> queueIds, CancellationToken _) => queueIds
                    .Where(_heads.ContainsKey)
                    .Select(queueId => _heads[queueId])
                    .ToArray());

            var queueManager = new Mock<IActivityQueueManager>();
            queueManager
                .Setup(manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string queueId, CancellationToken _) => _queues.GetValueOrDefault(queueId));

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_now);

            Selector = new AgentWorkSelector(queueItemStore.Object, queueManager.Object, clock.Object);
        }

        public AgentWorkSelector Selector { get; }

        public void AddQueue(
            string queueId,
            DateTime? waitingSince,
            InteractionPriority priority = InteractionPriority.Normal,
            int slaThresholdSeconds = 0,
            bool enableSlaAging = false)
        {
            _queues[queueId] = new ActivityQueue
            {
                ItemId = queueId,
                EnableSlaAging = enableSlaAging,
                SlaThresholdSeconds = slaThresholdSeconds,
            };

            if (waitingSince is not null)
            {
                _heads[queueId] = new QueueItem
                {
                    ItemId = $"item-{queueId}",
                    QueueId = queueId,
                    EnqueuedUtc = waitingSince.Value,
                    Priority = priority,
                };
            }
        }
    }
}
