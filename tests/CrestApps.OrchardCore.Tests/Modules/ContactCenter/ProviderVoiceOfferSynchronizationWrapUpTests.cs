using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Guards the bug where an agent's wrap-up was ended by reconciliation 32 ms after it began. A queued call handed from
/// the automated voice agent was answered by an agent and ended normally; the call's end started wrap-up, and the
/// reconciliation that runs on every CallEnded then took the call for a pre-connect offer (its queue item was no
/// longer Assigned and no reservation was still Accepted) and moved the agent straight back to Available with
/// Source=Reconciled. Reconciliation may only release offers and reservations that never connected.
/// </summary>
public sealed class ProviderVoiceOfferSynchronizationWrapUpTests
{
    private static readonly DateTime _answeredUtc = new(2026, 9, 24, 14, 4, 49, DateTimeKind.Utc);
    private static readonly DateTime _endedUtc = new(2026, 9, 24, 14, 6, 49, DateTimeKind.Utc);

    [Theory]
    // The live case: the queue item had already left Assigned by the time reconciliation read it.
    [InlineData(QueueItemStatus.Completed)]
    // Or it cannot be found at all.
    [InlineData(null)]
    public async Task ReconcileEndedOfferAsync_WhenAnAnsweredCallAlreadyStartedWrapUp_LeavesTheAgentInWrapUp(QueueItemStatus? queueItemStatus)
    {
        // Arrange
        var interaction = CreateInteraction(wrapUpStarted: true);
        var session = CreateSession(agentLegAnswered: true);
        var harness = new Harness(interaction, session, queueItemStatus, AgentPresenceStatus.WrapUp);

        // Act
        await harness.Service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        harness.Transitions.Verify(
            service => service.TransitionAsync(It.IsAny<AgentProfile>(), It.IsAny<AgentPresenceStatus>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Presence.Verify(
            manager => manager.CompleteWorkAsync(It.IsAny<string>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        Assert.Equal(AgentPresenceStatus.WrapUp, harness.Agent.PresenceStatus);
        Assert.False(harness.WorkStateReleased);
    }

    [Fact]
    public async Task ReconcileEndedOfferAsync_WhenAnAgentLegWasJoinedToAQueuedCall_StartsWrapUpInsteadOfReleasingTheAgent()
    {
        // Arrange
        // The live path has not started wrap-up yet (for example a reconciliation sweep ended the call), but the call
        // topology says an agent was joined to the caller: this was a handled call, not a pre-connect offer.
        var interaction = CreateInteraction(wrapUpStarted: false);
        var session = CreateSession(agentLegAnswered: true);
        var harness = new Harness(interaction, session, QueueItemStatus.Completed, AgentPresenceStatus.Busy);

        // Act
        await harness.Service.ReconcileEndedOfferAsync("int1", TestContext.Current.CancellationToken);

        // Assert
        harness.Transitions.Verify(
            service => service.TransitionAsync(It.IsAny<AgentProfile>(), It.IsAny<AgentPresenceStatus>(), It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Never);
        harness.Presence.Verify(
            manager => manager.StartWrapUpAsync("agent-1", It.IsAny<AgentStateChangeContext>(), It.IsAny<CancellationToken>()),
            Times.Once);
        Assert.False(harness.WorkStateReleased);
    }

    private static Interaction CreateInteraction(bool wrapUpStarted)
        => new Interaction
        {
            ItemId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            AnsweredUtc = _answeredUtc,
            EndedUtc = _endedUtc,
            WrapUpStartedUtc = wrapUpStarted ? _endedUtc : null,
        }.RestorePersistedStatus(InteractionStatus.Ended);

    private static CallSession CreateSession(bool agentLegAnswered)
    {
        var session = new CallSession
        {
            ItemId = "session-1",
            InteractionId = "int1",
            ActivityItemId = "act1",
            AgentId = "agent-1",
            QueueId = "queue-1",
            ProviderCallId = "caller-1",
            AnsweredUtc = _answeredUtc,
            EndedUtc = _endedUtc,
        }.RestorePersistedState(VoiceCallState.Ended);

        if (agentLegAnswered)
        {
            CallTopologyProjector.UpsertLeg(session, "agent-leg-1", CallPartyRole.Agent, CallLegStatus.Answered, _answeredUtc, agentId: "agent-1");
            CallTopologyProjector.EndRemainingLegs(session, _endedUtc);
        }

        return session;
    }

    private sealed class Harness
    {
        public Harness(Interaction interaction, CallSession session, QueueItemStatus? queueItemStatus, AgentPresenceStatus agentStatus)
        {
            Agent = new AgentProfile
            {
                ItemId = "agent-1",
                PresenceStatus = agentStatus,
                QueueIds = ["queue-1"],
            };

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(manager => manager.FindByIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);

            var callSessionManager = new Mock<ICallSessionManager>();
            callSessionManager.Setup(manager => manager.FindByInteractionIdAsync("int1", It.IsAny<CancellationToken>())).ReturnsAsync(session);

            var queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager
                .Setup(manager => manager.FindByActivityIdAsync("act1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(queueItemStatus.HasValue
                    ? new QueueItem { ItemId = "queue-item-1", ActivityItemId = "act1" }.RestorePersistedStatus(queueItemStatus.Value)
                    : null);

            var reservationManager = new Mock<IActivityReservationManager>();
            reservationManager.Setup(manager => manager.GetActiveByActivityAsync("act1", It.IsAny<CancellationToken>())).ReturnsAsync([]);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager.Setup(manager => manager.FindByIdAsync("agent-1", It.IsAny<CancellationToken>())).ReturnsAsync(Agent);

            var activityManager = new Mock<IOmnichannelActivityManager>();
            var workState = new Mock<IContactCenterWorkStateService>();
            workState
                .Setup(service => service.MutateAsync(It.IsAny<string>(), It.IsAny<Action<ContactCenterWorkState>>(), It.IsAny<CancellationToken>()))
                .Callback(() => WorkStateReleased = true)
                .ReturnsAsync((ContactCenterWorkState)null);

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider.Setup(provider => provider.GetService(typeof(IAgentPresenceManager))).Returns(Presence.Object);
            serviceProvider.Setup(provider => provider.GetService(typeof(IAgentStateTransitionService))).Returns(Transitions.Object);

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_endedUtc.AddMilliseconds(32));

            Service = new ProviderVoiceOfferSynchronizationService(
                interactionManager.Object,
                callSessionManager.Object,
                queueItemManager.Object,
                reservationManager.Object,
                agentManager.Object,
                activityManager.Object,
                workState.Object,
                serviceProvider.Object,
                new Lazy<IContactCenterAuditRecorder>(new RecordingContactCenterAuditRecorder()),
                clock.Object,
                new Mock<Microsoft.Extensions.Logging.ILogger<ProviderVoiceOfferSynchronizationService>>().Object);
        }

        public ProviderVoiceOfferSynchronizationService Service { get; }

        public AgentProfile Agent { get; }

        public Mock<IAgentPresenceManager> Presence { get; } = new();

        public Mock<IAgentStateTransitionService> Transitions { get; } = new();

        public bool WorkStateReleased { get; private set; }
    }
}
