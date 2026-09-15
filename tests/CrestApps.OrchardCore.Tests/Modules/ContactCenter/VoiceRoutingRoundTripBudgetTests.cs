using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Every inbound call asks who on the queue can take it, and the answer needs three facts about each candidate:
/// their profile, whether their browser is still there, and how much they are already carrying. Asking per agent
/// makes answering one call cost as much as the team is large — two hundred round trips before the caller hears
/// anything, on a path that runs on every call. The reads are batched today; this is what says so, because a
/// per-agent version would return the identical answer and no functional test could tell the difference.
/// </summary>
public sealed class VoiceRoutingRoundTripBudgetTests
{
    private const int AgentCount = 200;

    [Fact]
    public async Task AskingWhoCanTakeACall_CostsTheSameForTwoAgentsAsForTwoHundred()
    {
        // Arrange
        var probe = new AvailabilityProbe(AgentCount);

        // Act
        await probe.Service.GetForQueueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        probe.SessionManager.Verify(
            manager => manager.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);

        probe.InteractionManager.Verify(
            manager => manager.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AskingWhoCanTakeACall_NeverFallsBackToThePerAgentReads()
    {
        // Arrange
        // These are the single-item APIs the batched reads replaced. A loop over them is the regression, and it
        // is invisible in behaviour: same agents, same order, same answer.
        var probe = new AvailabilityProbe(AgentCount);

        // Act
        await probe.Service.GetForQueueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        probe.InteractionManager.Verify(
            manager => manager.CountActiveByAgentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);

        probe.AgentManager.Verify(
            manager => manager.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AnEmptyQueue_AsksNothingAtAll()
    {
        // Arrange
        // A queue nobody is signed into is the common case out of hours, and it should cost one read, not three.
        var probe = new AvailabilityProbe(agentCount: 0);

        // Act
        var available = await probe.Service.GetForQueueAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(available);

        probe.SessionManager.Verify(
            manager => manager.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private sealed class AvailabilityProbe
    {
        private static readonly DateTime _now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public AvailabilityProbe(int agentCount)
        {
            var agents = Enumerable.Range(0, agentCount)
                .Select(i => new AgentProfile
                {
                    ItemId = $"agent-{i:000}",
                    UserId = $"user-{i:000}",
                    MaxConcurrentInteractions = 1,
                })
                .ToArray();

            var sessions = agents
                .Select(agent => new AgentSession
                {
                    UserId = agent.UserId,
                    IsOnline = true,
                    LastHeartbeatUtc = _now,
                    ConnectionIds = ["connection-1"],
                    QueueIds = ["queue-1"],
                })
                .ToArray();

            AgentManager = new Mock<IAgentProfileManager>();
            AgentManager.Setup(x => x.GetAvailableForQueueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(agents);

            SessionManager = new Mock<IAgentSessionManager>();
            SessionManager.Setup(x => x.GetByUserIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(sessions);

            InteractionManager = new Mock<IInteractionManager>();
            InteractionManager.Setup(x => x.CountActiveByAgentIdsAsync(It.IsAny<IReadOnlyCollection<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((IReadOnlyCollection<string> ids, CancellationToken _) =>
                    ids.ToDictionary(id => id, _ => 0, StringComparer.Ordinal));

            var clock = new Mock<IClock>();
            clock.SetupGet(x => x.UtcNow).Returns(_now);

            Service = new AgentAvailabilityService(
                AgentManager.Object,
                SessionManager.Object,
                InteractionManager.Object,
                new OptionsWrapper<AgentAvailabilityOptions>(new AgentAvailabilityOptions()),
                clock.Object);
        }

        public AgentAvailabilityService Service { get; }

        public Mock<IAgentProfileManager> AgentManager { get; }

        public Mock<IAgentSessionManager> SessionManager { get; }

        public Mock<IInteractionManager> InteractionManager { get; }
    }
}
