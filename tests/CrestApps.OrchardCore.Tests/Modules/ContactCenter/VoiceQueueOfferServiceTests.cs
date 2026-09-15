using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Offering a call to an agent. The two entry points — take the next call off a queue, and ring one named agent —
/// did the same bookkeeping in two copies: resolve the agent, find the interaction, refuse the ones that cannot
/// be offered, and re-offer the interaction. These characterize both paths so the shared step can be pulled out
/// without changing which calls reach an agent and which are released.
/// </summary>
public sealed class VoiceQueueOfferServiceTests
{
    [Fact]
    public async Task OfferingFromAQueue_RingsTheReservedAgent()
    {
        // Arrange
        var harness = new OfferHarness();
        harness.Reserve("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithInteraction("activity-1", InteractionStatus.Ringing);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-1", userId);
        Assert.Equal("agent-1", harness.Interaction.AgentId);
        Assert.Equal("queue-1", harness.Interaction.QueueId);
    }

    [Fact]
    public async Task OfferingToOneAgent_RingsThatAgent()
    {
        // Arrange
        var harness = new OfferHarness();
        harness.ReserveSpecific("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithInteraction("activity-1", InteractionStatus.Ringing);

        // Act
        var userId = await harness.Service.OfferToAgentAsync("activity-1", "queue-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-1", userId);
    }

    [Fact]
    public async Task WhenTheReservedAgentHasNoAccount_TheReservationIsReleased()
    {
        // Arrange
        // A reservation held for an agent nobody can ring is capacity taken away from the agents who could have
        // taken the call.
        var harness = new OfferHarness();
        harness.Reserve("agent-1", "activity-1");
        harness.WithAgent("agent-1", userId: null);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertReservationReleased();
    }

    [Fact]
    public async Task WhenTheReservedAgentHasNoAccount_ADirectOfferAlsoReleasesTheReservation()
    {
        // Arrange
        var harness = new OfferHarness();
        harness.ReserveSpecific("agent-1", "activity-1");
        harness.WithAgent("agent-1", userId: null);

        // Act
        var userId = await harness.Service.OfferToAgentAsync("activity-1", "queue-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertReservationReleased();
    }

    [Fact]
    public async Task WhenTheCallHasAlreadyEnded_ItIsReconciledRatherThanOffered()
    {
        // Arrange
        // Ringing an agent for a call that is already over wastes their time and puts a dead call on their screen.
        var harness = new OfferHarness();
        harness.ReserveSpecific("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithInteraction("activity-1", InteractionStatus.Ended);

        // Act
        var userId = await harness.Service.OfferToAgentAsync("activity-1", "queue-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertEndedOfferReconciled();
    }

    [Fact]
    public async Task WhenAQueuedCallHasAlreadyEnded_TheNextOneIsTriedInstead()
    {
        // Arrange
        // This is where the two paths genuinely differ: a queue has more calls behind the dead one, and stopping
        // at it would leave an available agent idle with work waiting.
        var harness = new OfferHarness();
        harness.ReserveSequence(
            ("agent-1", "activity-1"),
            ("agent-2", "activity-2"));
        harness.WithAgent("agent-1", "user-1");
        harness.WithAgent("agent-2", "user-2");
        harness.WithInteraction("activity-1", InteractionStatus.Ended);
        harness.WithInteraction("activity-2", InteractionStatus.Ringing);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-2", userId);
    }

    [Fact]
    public async Task WhenNothingIsWaiting_NoAgentIsRung()
    {
        // Arrange
        var harness = new OfferHarness();

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
    }

    [Fact]
    public async Task WhenAPreviewDialHasNoInteractionYet_TheReservationIsKept()
    {
        // Arrange
        // A preview dial reserves the agent before the call exists — that is the whole point of preview. Releasing
        // the reservation here would take the call away from the agent who was about to place it.
        var harness = new OfferHarness();
        harness.Reserve("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithActivity("activity-1", ActivitySources.PreviewDial);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertReservationNotReleased();
    }

    [Fact]
    public async Task WhenAnOutboundCallHasNoInteraction_TheReservationIsReleased()
    {
        // Arrange
        var harness = new OfferHarness();
        harness.Reserve("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithActivity("activity-1", ActivitySources.Manual);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertReservationReleased();
    }

    [Fact]
    public async Task WhenADirectOfferHasNoInteraction_TheReservationIsReleased()
    {
        // Arrange
        var harness = new OfferHarness();
        harness.ReserveSpecific("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");

        // Act
        var userId = await harness.Service.OfferToAgentAsync("activity-1", "queue-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
        harness.AssertReservationReleased();
    }

    [Fact]
    public async Task WhenTheNamedAgentCannotTakeTheCall_NothingIsOffered()
    {
        // Arrange
        // A direct offer fails closed: there is no fallback agent, because the caller asked for this one.
        var harness = new OfferHarness();

        // Act
        var userId = await harness.Service.OfferToAgentAsync("activity-1", "queue-1", "agent-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(userId);
    }

    [Fact]
    public async Task Offering_ReclaimsStaleReservationsFirst()
    {
        // Arrange
        // An offer an agent silently ignored holds capacity until the next sweep. On a busy queue that is the
        // difference between the next caller being answered and waiting a minute for a timer.
        var harness = new OfferHarness();

        // Act
        await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        harness.AssertReclaimAttempted();
    }

    [Fact]
    public async Task WhenTheReclaimFails_TheOfferStillHappens()
    {
        // Arrange
        // The reclaim is opportunistic. Letting its failure block the offer would turn a housekeeping hiccup into
        // a queue that stops ringing.
        var harness = new OfferHarness();
        harness.MakeReclaimThrow();
        harness.Reserve("agent-1", "activity-1");
        harness.WithAgent("agent-1", "user-1");
        harness.WithInteraction("activity-1", InteractionStatus.Ringing);

        // Act
        var userId = await harness.Service.OfferNextAsync("queue-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal("user-1", userId);
    }

    private sealed class OfferHarness
    {
        private readonly Queue<ActivityReservation> _reservations = new();
        private readonly Dictionary<string, AgentProfile> _agents = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Interaction> _interactions = new(StringComparer.Ordinal);
        private readonly Dictionary<string, OmnichannelActivity> _activities = new(StringComparer.Ordinal);
        private readonly Mock<IActivityAssignmentService> _assignment = new();
        private readonly Mock<IActivityReservationService> _reservationService = new();
        private readonly Mock<IActivityReservationReclaimer> _reclaimer = new();
        private readonly Mock<IProviderVoiceOfferSynchronizationService> _offerSynchronization = new();

        public OfferHarness()
        {
            _assignment.Setup(x => x.AssignNextAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _reservations.Count > 0 ? _reservations.Dequeue() : null);
            _assignment.Setup(x => x.AssignSpecificAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => _reservations.Count > 0 ? _reservations.Dequeue() : null);

            var agentManager = new Mock<IAgentProfileManager>();
            agentManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => _agents.GetValueOrDefault(id));

            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(x => x.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string activityId, CancellationToken _) =>
                {
                    Interaction = _interactions.GetValueOrDefault(activityId);

                    return Interaction;
                });

            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager.Setup(x => x.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => _activities.GetValueOrDefault(id));

            var workManager = new Mock<IContactCenterFeatureWorkManager>();
            workManager.Setup(x => x.TryEnter(It.IsAny<string>())).Returns(new NoOpWorkLease());

            Service = new VoiceQueueOfferService(
                _assignment.Object,
                _reservationService.Object,
                _reclaimer.Object,
                agentManager.Object,
                interactionManager.Object,
                activityManager.Object,
                _offerSynchronization.Object,
                workManager.Object,
                NullLogger<VoiceQueueOfferService>.Instance);
        }

        public VoiceQueueOfferService Service { get; }

        public Interaction Interaction { get; private set; }

        public void Reserve(string agentId, string activityId)
            => _reservations.Enqueue(NewReservation(agentId, activityId));

        public void ReserveSpecific(string agentId, string activityId)
            => _reservations.Enqueue(NewReservation(agentId, activityId));

        public void ReserveSequence(params (string AgentId, string ActivityId)[] reservations)
        {
            foreach (var (agentId, activityId) in reservations)
            {
                _reservations.Enqueue(NewReservation(agentId, activityId));
            }
        }

        public void WithAgent(string agentId, string userId)
            => _agents[agentId] = new AgentProfile { ItemId = agentId, UserId = userId };

        public void WithInteraction(string activityId, InteractionStatus status)
            => _interactions[activityId] = new Interaction { ItemId = $"interaction-{activityId}" }.RestorePersistedStatus(status);

        public void WithActivity(string activityId, string source)
            => _activities[activityId] = new OmnichannelActivity { ItemId = activityId, Source = source };

        public void MakeReclaimThrow()
            => _reclaimer.Setup(x => x.ReclaimDueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new InvalidOperationException("The reclaim pass could not run."));

        public void AssertReservationReleased()
            => _reservationService.Verify(x => x.RejectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);

        public void AssertReservationNotReleased()
            => _reservationService.Verify(x => x.RejectAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

        public void AssertEndedOfferReconciled()
            => _offerSynchronization.Verify(x => x.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);

        public void AssertReclaimAttempted()
            => _reclaimer.Verify(x => x.ReclaimDueAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Once);

        private static ActivityReservation NewReservation(string agentId, string activityId)
            => new()
            {
                ItemId = $"reservation-{activityId}",
                AgentId = agentId,
                ActivityItemId = activityId,
                QueueId = "queue-1",
            };

        private sealed class NoOpWorkLease : IContactCenterFeatureWorkLease
        {
            public void Dispose()
            {
            }
        }
    }
}
