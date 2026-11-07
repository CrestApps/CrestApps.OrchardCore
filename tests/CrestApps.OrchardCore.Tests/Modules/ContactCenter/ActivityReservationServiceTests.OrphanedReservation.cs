#nullable enable annotations

using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Telephony;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed partial class ActivityReservationServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReserveAsync_WhenAWaitingActivityStillHoldsAnOrphanedReservation_CancelsItAndReservesAgain(bool accepted)
    {
        // Arrange: the activity is waiting, yet a pending or accepted reservation still holds its unique claim. The
        // insert of a new reservation would violate that claim on every cycle and stop the queue from dialing.
        var orphan = new ActivityReservation { ItemId = "orphan-1", ActivityItemId = "act-1", AgentId = "gone" };
        orphan.TransitionTo(ReservationStatus.Pending);

        if (accepted)
        {
            orphan.TransitionTo(ReservationStatus.Accepted);
        }

        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityReservation());
        reservationManager
            .Setup(m => m.GetActiveByActivityAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([orphan]);
        var queueItemManager = new Mock<IQueueItemManager>();
        var agentManager = new Mock<IAgentProfileManager>();
        var queueManager = new Mock<IActivityQueueManager>();
        queueManager.Setup(m => m.FindByIdAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync(new ActivityQueue { ItemId = "q1" });
        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act-1", It.IsAny<CancellationToken>())).ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });
        var service = CreateService(
            reservationManager,
            queueItemManager,
            agentManager,
            queueManager,
            new Mock<IActivityQueueService>(),
            new Mock<IInteractionManager>(),
            activityManager,
            new Mock<IContactCenterEventPublisher>(),
            new Mock<ITelephonyService>());

        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", ActivityItemId = "act-1" };
        var agent = new AgentProfile { ItemId = "a1", UserId = "u1", PresenceStatus = AgentPresenceStatus.Available };
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>())).ReturnsAsync(agent);
        queueItemManager.Setup(m => m.FindByIdAsync("qi-1", It.IsAny<CancellationToken>())).ReturnsAsync(item);

        // Act
        var reservation = await service.ReserveAsync(item, agent, 30, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(ReservationStatus.Canceled, orphan.Status);
        Assert.NotNull(reservation);
        Assert.Equal(ReservationStatus.Pending, reservation.Status);
        Assert.Equal(QueueItemStatus.Reserved, item.Status);
    }
}
