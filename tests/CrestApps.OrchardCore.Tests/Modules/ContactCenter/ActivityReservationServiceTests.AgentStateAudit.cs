#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Routing's agent state changes (reserved, busy, and every release) are the ones the workforce reports used to
/// miss, so each is proved to record exactly one transition, tied to its reservation and interaction.
/// </summary>
public sealed partial class ActivityReservationServiceTests
{
    [Fact]
    public async Task ReserveAsync_RecordsOneReservedTransition_WithTheReservationAndInteraction()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation { ItemId = "r1" });
        var queueItemManager = new Mock<IQueueItemManager>();
        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var interactionManager = CreateInteractionManager("act-1", "int-1");
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, queueItemManager, agentManager, interactionManager, recorder);

        // Act
        await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Reserved, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Reserved, recorded.Change.Source);
        Assert.Equal("r1", recorded.Change.ReservationId);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(_now, recorded.Change.ChangedUtc);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Fact]
    public async Task AcceptAsync_RecordsOneBusyTransition_ByTheAgent()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Reserved, ActiveReservationId = "r1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(
            reservationManager,
            new Mock<IQueueItemManager>(),
            agentManager,
            CreateInteractionManager("act-1", "int-1"),
            recorder);

        // Act
        await service.AcceptAsync("r1", TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Reserved, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Busy, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Accepted, recorded.Change.Source);
        Assert.Equal("r1", recorded.Change.ReservationId);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Equal(ContactCenterActorType.Agent, recorded.Actor.Type);
        Assert.Equal("u1", recorded.Actor.Id);
    }

    [Fact]
    public async Task ExpireDueAsync_RecordsOneReleasedTransition_WithTheExpiredReason()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.GetExpiredAsync(_now, It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpiredReservationPage([reservation], null, 0));
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var queueItemManager = new Mock<IQueueItemManager>();
        queueItemManager
            .Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi-1" }.RestorePersistedStatus(QueueItemStatus.Reserved));
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            RequestedPresenceStatus = AgentPresenceStatus.Available,
            ActiveReservationId = "r1",
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, queueItemManager, agentManager, CreateInteractionManager("act-1", "int-1"), recorder);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Reserved, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Released, recorded.Change.Source);
        Assert.Equal(AgentReleaseReasons.Expired, recorded.Change.ReleaseReason);
        Assert.Equal("r1", recorded.Change.ReservationId);
        Assert.Equal("int-1", recorded.Change.InteractionId);
        Assert.Null(recorded.Change.ReasonName);
        Assert.Equal(ContactCenterActorType.System, recorded.Actor.Type);
    }

    [Fact]
    public async Task ExpireDueAsync_WhenTheAgentAskedForABreakWhileItRang_RecordsTheBreakWithItsReason()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager
            .Setup(m => m.GetExpiredAsync(_now, It.IsAny<DateTime?>(), It.IsAny<long>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ExpiredReservationPage([reservation], null, 0));
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = new AgentProfile
        {
            ItemId = "a1",
            UserId = "u1",
            PresenceStatus = AgentPresenceStatus.Reserved,
            RequestedPresenceStatus = AgentPresenceStatus.Break,
            PresenceRequestedUtc = _now.AddSeconds(-5),
            PresenceReason = "Lunch",
            PresenceReasonCodeId = "code-lunch",
            ActiveReservationId = "r1",
        };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), recorder);

        // Act
        await service.ExpireDueAsync(TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Break, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Released, recorded.Change.Source);
        Assert.Equal("code-lunch", recorded.Change.ReasonCodeId);
        Assert.Equal("Lunch", recorded.Change.ReasonName);
        Assert.Null(agent.PresenceRequestedUtc);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancelOrRejectAsync_RecordsTheReleaseReason(bool reject)
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Pending);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Reserved, ActiveReservationId = "r1" };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), recorder);

        // Act
        if (reject)
        {
            await service.RejectAsync("r1", TestContext.Current.CancellationToken);
        }
        else
        {
            await service.CancelAsync("r1", TestContext.Current.CancellationToken);
        }

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentStateChangeSources.Released, recorded.Change.Source);
        Assert.Equal(reject ? AgentReleaseReasons.Rejected : AgentReleaseReasons.Canceled, recorded.Change.ReleaseReason);
    }

    [Fact]
    public async Task CompensateAsync_RecordsOneReleasedTransition_AsCompensated()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", QueueItemId = "qi-1", AgentId = "a1", ActivityItemId = "act-1" }
            .RestorePersistedStatus(ReservationStatus.Accepted);
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>())).ReturnsAsync(reservation);
        reservationManager.Setup(m => m.GetActiveByAgentAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync([]);
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Busy };
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        var recorder = new RecordingAuditRecorder();
        var service = CreateAuditedService(reservationManager, new Mock<IQueueItemManager>(), agentManager, CreateInteractionManager("act-1", "int-1"), recorder);

        // Act
        await service.CompensateAsync("r1", removeFromQueue: true, TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.StateChanges);
        Assert.Equal(AgentPresenceStatus.Busy, recorded.Change.PreviousState);
        Assert.Equal(AgentPresenceStatus.Available, recorded.Change.CurrentState);
        Assert.Equal(AgentStateChangeSources.Released, recorded.Change.Source);
        Assert.Equal(AgentReleaseReasons.Compensated, recorded.Change.ReleaseReason);
        Assert.Equal("int-1", recorded.Change.InteractionId);
    }

    private static Mock<IInteractionManager> CreateInteractionManager(string activityItemId, string interactionId)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(m => m.FindByActivityIdAsync(activityItemId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = interactionId, ActivityItemId = activityItemId }.RestorePersistedStatus(InteractionStatus.Ringing));

        return interactionManager;
    }

    private static ActivityReservationService CreateAuditedService(
        Mock<IActivityReservationManager> reservationManager,
        Mock<IQueueItemManager> queueItemManager,
        Mock<IAgentProfileManager> agentManager,
        Mock<IInteractionManager> interactionManager,
        RecordingAuditRecorder recorder)
    {
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager
            .Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        return CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            new Mock<IActivityQueueManager>(),
            new Mock<IActivityQueueService>(),
            interactionManager,
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            providerCommandStateService: null,
            scopeExecutor: null,
            auditRecorder: recorder);
    }
}
