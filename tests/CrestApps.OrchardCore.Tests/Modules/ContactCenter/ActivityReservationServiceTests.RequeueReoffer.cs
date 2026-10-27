using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// An offer that rang out and put its caller back into the queue publishes the requeue that offers the next agent in
/// line at once, rather than leaving the caller to the minute's assignment pass.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Theory]
    [InlineData(UnansweredOfferAction.Requeue)]
    [InlineData(UnansweredOfferAction.Voicemail)]
    [InlineData(UnansweredOfferAction.Reject)]
    public async Task ExpireDueAsync_WhenTheCallerGoesBackIntoTheQueue_PublishesTheRequeueAfterTheRelease(UnansweredOfferAction configuredAction)
    {
        // Arrange
        // A queue set to voicemail or reject falls back to requeueing when there is no provider command
        // infrastructure; that caller waits for the next agent just the same.
        var scenario = new RangOutScenario("q1", configuredAction, InteractionStatus.Ringing);

        // Act
        await scenario.Service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(QueueItemStatus.Waiting, scenario.QueueItem.Status);
        var requeued = Assert.Single(scenario.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.OfferRequeued);
        Assert.Equal(nameof(ActivityReservation), requeued.AggregateType);
        Assert.Equal("r1", requeued.AggregateId);
        Assert.Equal("q1", requeued.GetData<OfferDeclinedEventData>()?.QueueId);

        // The release goes first, so the leg rung for the expired offer is hung up before the next agent's rings.
        Assert.True(
            scenario.PublishedEvents.FindIndex(e => e.EventType == ContactCenterConstants.Events.AgentReleased) <
            scenario.PublishedEvents.IndexOf(requeued));
    }

    [Theory]
    [InlineData(ContactCenterConstants.DirectRouting.QueueId, InteractionStatus.Ringing)]
    [InlineData(ContactCenterConstants.CampaignQueue.Prefix + "c1", InteractionStatus.Ringing)]
    [InlineData("q1", InteractionStatus.Ended)]
    public async Task ExpireDueAsync_WhenNoNextAgentIsWaitingForTheCaller_DoesNotPublishARequeue(string queueId, InteractionStatus interactionStatus)
    {
        // Arrange
        // A direct line rings only its own agent, a campaign's virtual queue is paced by the dialer, and a caller who
        // hung up while the offer rang is not waiting for anybody.
        var scenario = new RangOutScenario(queueId, UnansweredOfferAction.Requeue, interactionStatus);

        // Act
        await scenario.Service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Expired, scenario.Reservation.Status);
        Assert.Contains(scenario.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentReleased);
        Assert.DoesNotContain(scenario.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.OfferRequeued);
    }

    private sealed class RangOutScenario
    {
        public RangOutScenario(string queueId, UnansweredOfferAction unansweredOfferAction, InteractionStatus interactionStatus)
        {
            Reservation = new ActivityReservation { ItemId = "r1", QueueId = queueId, QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
                .RestorePersistedStatus(ReservationStatus.Pending);
            QueueItem = new QueueItem { ItemId = "qi-1", QueueId = queueId, ReservationId = "r1", AgentId = "a1" }
                .RestorePersistedStatus(QueueItemStatus.Reserved);

            var reservationManager = new Mock<IActivityReservationManager>();
            reservationManager.Setup(m => m.GetExpiredAsync(_now, It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ExpiredReservationPage([Reservation], null, 0));
            reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);
            var queueItemManager = new Mock<IQueueItemManager>();
            queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(QueueItem);
            var agentManager = new Mock<IAgentProfileManager>();
            agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1", QueueIds = ["q1"], PresenceStatus = AgentPresenceStatus.Reserved });
            var queueManager = new Mock<IActivityQueueManager>();
            queueManager.Setup(m => m.FindByIdAsync(queueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new ActivityQueue { ItemId = queueId, Name = "Voice", UnansweredOfferAction = unansweredOfferAction });
            var interaction = new Interaction { ItemId = "i1", ActivityItemId = "act-1", ProviderName = "provider-1", ProviderInteractionId = "call-1", AgentId = "a1" }
                .RestorePersistedStatus(interactionStatus);
            var interactionManager = new Mock<IInteractionManager>();
            interactionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(interaction);
            var activityManager = new Mock<IOmnichannelActivityManager>();
            activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1", AssignmentStatus = ActivityAssignmentStatus.Reserved, Status = ActivityStatus.Pending, ReservationId = "r1" });
            var publisher = new Mock<IContactCenterEventPublisher>();
            publisher
                .Setup(p => p.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => PublishedEvents.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            // No provider command infrastructure, so a voicemail or reject action falls back to requeueing.
            Service = CreateService(
                reservationManager,
                queueItemManager,
                agentManager,
                queueManager,
                new Mock<IActivityQueueService>(),
                interactionManager,
                activityManager,
                publisher,
                providerCommandStateService: null,
                scopeExecutor: null);
        }

        public ActivityReservation Reservation { get; }

        public QueueItem QueueItem { get; }

        public ActivityReservationService Service { get; }

        public List<InteractionEvent> PublishedEvents { get; } = [];
    }
}
