#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Moq;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Releasing an accepted offer whose agent could not be reached: the caller follows the queue's unanswered-offer
/// action, as an offer that rang out does, and the agent is set Away.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task ReleaseUnreachableAgentAsync_WhenTheQueueSendsUnansweredOffersToVoicemail_SendsTheCallerToVoicemailAndSetsTheAgentAway()
    {
        // Arrange
        var fixture = new UnreachableAgentFixture(UnansweredOfferAction.Voicemail);
        var providerCommandStateService = new Mock<IProviderCommandStateService>(MockBehavior.Strict);
        ProviderCommandRegistration? capturedRegistration = null;
        providerCommandStateService
            .Setup(m => m.RegisterAsync(It.IsAny<ProviderCommandRegistration>(), It.IsAny<CancellationToken>()))
            .Callback<ProviderCommandRegistration, CancellationToken>((registration, _) => capturedRegistration = registration)
            .ReturnsAsync(new ProviderCommand());
        Func<IProviderCommandProcessor, Task>? scheduledDispatch = null;
        var scopeExecutor = new Mock<IContactCenterScopeExecutor>(MockBehavior.Strict);
        scopeExecutor
            .Setup(m => m.ScheduleAfterCommit<IProviderCommandProcessor>(It.IsAny<Func<IProviderCommandProcessor, Task>>()))
            .Callback<Func<IProviderCommandProcessor, Task>>(operation => scheduledDispatch = operation)
            .Returns(true);
        var service = fixture.CreateService(providerCommandStateService, scopeExecutor);

        // Act
        var released = await service.ReleaseUnreachableAgentAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(fixture.Reservation, released);
        Assert.Equal(ReservationStatus.Canceled, fixture.Reservation.Status);
        Assert.Equal(_now, fixture.Reservation.ModifiedUtc);

        // The caller leaves the queue for voicemail, the way an offer that rang out on this queue would send them.
        Assert.Equal(QueueItemStatus.Removed, fixture.QueueItem.Status);
        Assert.Null(fixture.QueueItem.ReservationId);
        Assert.Null(fixture.QueueItem.AgentId);
        Assert.Equal(_now, fixture.QueueItem.DequeuedUtc);
        Assert.Equal(InteractionStatus.Ringing, fixture.Interaction.Status);
        Assert.Null(fixture.Interaction.AgentId);
        Assert.Null(fixture.Interaction.EndedUtc);
        Assert.Equal("Voicemail", fixture.Interaction.TechnicalMetadata["unansweredOfferAction"]);
        Assert.NotNull(capturedRegistration);
        Assert.Equal(
            capturedRegistration!.CommandId,
            Assert.IsType<string>(fixture.Interaction.TechnicalMetadata[ContactCenterConstants.CommandMetadata.CommandId]));
        AssertCallActionRegistration(
            capturedRegistration!,
            ProviderCommandType.SendToVoicemail,
            fixture.Interaction,
            fixture.Reservation,
            fixture.Queue,
            fixture.Agent,
            "call-1");
        await AssertScheduledDispatchAsync(scheduledDispatch, capturedRegistration!.CommandId);

        // The assignment the accept made is undone with the offer.
        Assert.Equal(ActivityAssignmentStatus.Released, fixture.Activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Completed, fixture.Activity.Status);
        Assert.Null(fixture.Activity.ReservationId);
        Assert.Null(fixture.Activity.AssignedToId);
        Assert.Null(fixture.Activity.AssignedToUsername);
        Assert.Null(fixture.Activity.AssignedToUtc);

        // The agent is not returned to work: their phone cannot ring, so they are Away until they come back.
        Assert.Equal(AgentPresenceStatus.Away, fixture.Agent.PresenceStatus);
        Assert.Equal(AgentPresenceReasons.PhoneUnreachable, fixture.Agent.PresenceReason);
        Assert.Null(fixture.Agent.PresenceReasonCodeId);
        Assert.Null(fixture.Agent.RequestedPresenceStatus);
        Assert.Null(fixture.Agent.ActiveReservationId);
        Assert.Null(fixture.Agent.IdleSinceUtc);

        // Routing learns the release and the agent's screens learn the new state; the accepted offer was already
        // settled when the agent took it, so no second settlement is recorded.
        Assert.Contains(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentReleased);
        var presence = Assert.Single(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentPresenceChanged);
        var presenceData = presence.GetData<AgentPresenceChangedEventData>();
        Assert.NotNull(presenceData);
        Assert.Equal(AgentPresenceStatus.Busy, presenceData!.PreviousStatus);
        Assert.Equal(AgentPresenceStatus.Away, presenceData.CurrentStatus);
        Assert.Equal(AgentPresenceReasons.PhoneUnreachable, presenceData.Reason);
        Assert.DoesNotContain(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.OfferRequeued);
        Assert.Empty(fixture.AuditRecorder.Offers);
        fixture.Session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ReleaseUnreachableAgentAsync_WhenTheQueueRequeuesUnansweredOffers_ReturnsTheCallerToTheQueueAndSetsTheAgentAway()
    {
        // Arrange
        var fixture = new UnreachableAgentFixture(UnansweredOfferAction.Requeue);
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableAgentAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Same(fixture.Reservation, released);
        Assert.Equal(ReservationStatus.Canceled, fixture.Reservation.Status);

        // The caller waits for the next agent, as an offer that rang out on this queue would leave them.
        Assert.Equal(QueueItemStatus.Waiting, fixture.QueueItem.Status);
        Assert.Null(fixture.QueueItem.ReservationId);
        Assert.Null(fixture.QueueItem.AgentId);
        Assert.Equal(InteractionStatus.Created, fixture.Interaction.Status);
        Assert.Null(fixture.Interaction.AgentId);
        Assert.Null(fixture.Interaction.EndedUtc);
        fixture.QueueService.Verify(s => s.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()), Times.Never);

        Assert.Equal(ActivityAssignmentStatus.Available, fixture.Activity.AssignmentStatus);
        Assert.Equal(ActivityStatus.Pending, fixture.Activity.Status);
        Assert.Null(fixture.Activity.ReservationId);
        Assert.Null(fixture.Activity.AssignedToId);
        Assert.Null(fixture.Activity.AssignedToUsername);
        Assert.Null(fixture.Activity.AssignedToUtc);

        Assert.Equal(AgentPresenceStatus.Away, fixture.Agent.PresenceStatus);
        Assert.Equal(AgentPresenceReasons.PhoneUnreachable, fixture.Agent.PresenceReason);
        Assert.Null(fixture.Agent.RequestedPresenceStatus);
        Assert.Null(fixture.Agent.ActiveReservationId);
        Assert.Contains(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentReleased);
        Assert.Contains(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.AgentPresenceChanged);
        Assert.Empty(fixture.AuditRecorder.Offers);

        // The next agent in line is offered the caller now, not at the minute's assignment pass.
        var requeued = Assert.Single(fixture.PublishedEvents, e => e.EventType == ContactCenterConstants.Events.OfferRequeued);
        Assert.Equal("r1", requeued.AggregateId);
        Assert.Equal("q1", requeued.GetData<OfferDeclinedEventData>()?.QueueId);
    }

    [Fact]
    public async Task ReleaseUnreachableAgentAsync_RecordsOneReleasedTransition_FromBusyToAwayWithTheUnreachableReason()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueId = "q1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Accepted);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi-1", ReservationId = "r1", AgentId = "a1" }.RestorePersistedStatus(QueueItemStatus.Assigned));
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Busy,
            RequestedPresenceStatus = AgentPresenceStatus.Available,
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, queueItemManager, agentManager, CreateInteractionManager("act-1", "int-1"), recorder);

        // Act
        var released = await service.ReleaseUnreachableAgentAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(released);
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Busy, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Away, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Released, recorded.Change.Source);
        Assert.Equal(AgentReleaseReasons.Unreachable, recorded.Change.ReleaseReason);
        Assert.Equal(AgentPresenceReasons.PhoneUnreachable, recorded.Change.ReasonName);
        Assert.Null(recorded.Change.RequestedState);
        Assert.Equal("r1", recorded.Change.ReservationId);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(_now, recorded.Change.ChangedUtc);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Theory]
    [InlineData(ReservationStatus.Pending)]
    [InlineData(ReservationStatus.Expired)]
    [InlineData(ReservationStatus.Canceled)]
    public async Task ReleaseUnreachableAgentAsync_WhenTheReservationIsNotAccepted_DoesNothing(ReservationStatus status)
    {
        // Arrange
        // An offer still ringing rings out on its own; one already settled was decided by whatever settled it, so the
        // redelivered failure of an offer released earlier releases nobody a second time.
        var fixture = new UnreachableAgentFixture(UnansweredOfferAction.Voicemail);
        fixture.Reservation.RestorePersistedStatus(status);
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableAgentAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(released);
        Assert.Equal(status, fixture.Reservation.Status);
        Assert.Equal(QueueItemStatus.Assigned, fixture.QueueItem.Status);
        Assert.Equal(AgentPresenceStatus.Busy, fixture.Agent.PresenceStatus);
        Assert.Equal("a1", fixture.Interaction.AgentId);
        Assert.Empty(fixture.PublishedEvents);
        fixture.Session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(ContactCenterConstants.DirectRouting.QueueId)]
    [InlineData("__campaign-queue__campaign-1")]
    public async Task ReleaseUnreachableAgentAsync_WhenTheReservationIsOnAPersonalLineOrACampaignQueue_LeavesItAlone(string queueId)
    {
        // Arrange
        // A direct line has no queue to decide for the caller, and a campaign's call has its own abandonment handling.
        var fixture = new UnreachableAgentFixture(UnansweredOfferAction.Voicemail, queueId);
        var service = fixture.CreateService();

        // Act
        var released = await service.ReleaseUnreachableAgentAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Null(released);
        Assert.Equal(ReservationStatus.Accepted, fixture.Reservation.Status);
        Assert.Equal(AgentPresenceStatus.Busy, fixture.Agent.PresenceStatus);
        Assert.Empty(fixture.PublishedEvents);
        fixture.Session.Verify(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// An inbound caller whose offer the agent accepted: the reservation is accepted, the queue item assigned, the
    /// agent Busy with the state to return to captured, the interaction ringing the agent, and the activity assigned.
    /// </summary>
    private sealed class UnreachableAgentFixture
    {
        public UnreachableAgentFixture(UnansweredOfferAction unansweredOfferAction, string queueId = "q1")
        {
            Reservation = new ActivityReservation
            {
                ItemId = "r1",
                QueueId = queueId,
                QueueItemId = "qi-1",
                AgentId = "a1",
                ActivityItemId = "act-1",
                CreatedUtc = _now.AddSeconds(-20),
            }.RestorePersistedStatus(ReservationStatus.Accepted);
            ReservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(Reservation);

            QueueItem = new QueueItem
            {
                ItemId = "qi-1",
                QueueId = queueId,
                ActivityItemId = "act-1",
                ReservationId = "r1",
                AgentId = "a1",
            }.RestorePersistedStatus(QueueItemStatus.Assigned);
            QueueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(QueueItem);

            Agent = new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
                UserName = "agent",
                DisplayName = "Agent",
                QueueIds = ["q1"],
                PresenceStatus = AgentPresenceStatus.Busy,
                RequestedPresenceStatus = AgentPresenceStatus.Available,
                IdleSinceUtc = _now.AddMinutes(-10),
            };
            AgentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(Agent);

            Queue = new ActivityQueue { ItemId = queueId, Name = "Voice", UnansweredOfferAction = unansweredOfferAction };
            QueueManager.Setup(m => m.FindByIdAsync(queueId, It.IsAny<CancellationToken>())).ReturnsAsync(Queue);

            QueueService
                .Setup(m => m.DequeueAsync(QueueItem, QueueItemStatus.Removed, It.IsAny<CancellationToken>()))
                .Callback(() => QueueItem.RestorePersistedStatus(QueueItemStatus.Removed))
                .Returns(Task.CompletedTask);

            Interaction = new Interaction
            {
                ItemId = "i1",
                ActivityItemId = "act-1",
                QueueId = queueId,
                ProviderName = "provider-1",
                ProviderInteractionId = "call-1",
                AgentId = "a1",
                Direction = InteractionDirection.Inbound,
            }.RestorePersistedStatus(InteractionStatus.Ringing);
            InteractionManager.Setup(m => m.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(Interaction);

            Activity = new OmnichannelActivity
            {
                ItemId = "act-1",
                AssignmentStatus = ActivityAssignmentStatus.Assigned,
                Status = ActivityStatus.Pending,
                ReservationId = "r1",
                ReservedById = "u1",
                ReservedByUsername = "agent",
                AssignedToId = "u1",
                AssignedToUsername = "agent",
                AssignedToUtc = _now.AddSeconds(-10),
            };
            ActivityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(Activity);

            Publisher
                .Setup(m => m.PublishAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) => PublishedEvents.Add(interactionEvent))
                .Returns(Task.CompletedTask);

            Session
                .Setup(s => s.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        public ActivityReservation Reservation { get; }

        public QueueItem QueueItem { get; }

        public AgentProfile Agent { get; }

        public ActivityQueue Queue { get; }

        public Interaction Interaction { get; }

        public OmnichannelActivity Activity { get; }

        public List<InteractionEvent> PublishedEvents { get; } = [];

        public RecordingContactCenterAuditRecorder AuditRecorder { get; } = new();

        public Mock<IActivityReservationManager> ReservationManager { get; } = new();

        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IAgentProfileManager> AgentManager { get; } = new();

        public Mock<IActivityQueueManager> QueueManager { get; } = new();

        public Mock<IActivityQueueService> QueueService { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IOmnichannelActivityManager> ActivityManager { get; } = new();

        public Mock<IContactCenterEventPublisher> Publisher { get; } = new();

        public Mock<ISession> Session { get; } = new();

        public ActivityReservationService CreateService(
            Mock<IProviderCommandStateService>? providerCommandStateService = null,
            Mock<IContactCenterScopeExecutor>? scopeExecutor = null)
            => ActivityReservationServiceTests.CreateService(
                ReservationManager,
                QueueItemManager,
                AgentManager,
                QueueManager,
                QueueService,
                InteractionManager,
                ActivityManager,
                Publisher,
                providerCommandStateService,
                scopeExecutor,
                session: Session,
                auditRecorder: AuditRecorder);
    }
}
