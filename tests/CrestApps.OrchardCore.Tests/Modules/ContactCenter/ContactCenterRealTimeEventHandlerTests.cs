using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using CrestApps.OrchardCore.ContactCenter.Hubs;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using CrestApps.OrchardCore.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Users;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRealTimeEventHandlerTests
{
    private static readonly DateTime _now = new(2026, 6, 30, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task HandleAsync_AgentPresenceChanged_BroadcastsPresence()
    {
        // Arrange
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile
            {
                ItemId = "a1",
                UserId = "u1",
                DisplayName = "Agent One",
                PresenceStatus = AgentPresenceStatus.Available,
                QueueIds = ["q1"],
            });

        var notifier = new Mock<IContactCenterRealTimeNotifier>();
        var handler = CreateHandler(notifier, agentManager: agentManager);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentPresenceChanged,
            AggregateId = "a1",
            OccurredUtc = _now,
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        notifier.Verify(
            n => n.NotifyPresenceChangedAsync(
                It.Is<AgentPresenceNotification>(p => p.UserId == "u1" && p.Status == "Available"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Theory]
    [InlineData(ActivitySources.PowerDial, true)]
    [InlineData(ActivitySources.Inbound, false)]
    public async Task HandleAsync_AgentReserved_BroadcastsOfferAndQueueStats(
        string activitySource,
        bool expectedAutoOpen)
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                AgentId = "a1",
                ActivityItemId = "act1",
                QueueItemId = "qi1",
                QueueId = "q1",
                ExpiresUtc = _now.AddSeconds(30),
            });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1" });

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore.Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ItemId = "qi1" }, new QueueItem { ItemId = "qi2" }]);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.FindByIdAsync("act1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity
            {
                ItemId = "act1",
                Source = activitySource,
            });

        var notifier = new Mock<IContactCenterRealTimeNotifier>();
        var handler = CreateHandler(notifier, reservationManager, agentManager, queueItemStore, activityManager);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.AgentReserved,
            AggregateId = "r1",
            OccurredUtc = _now,
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        notifier.Verify(
            n => n.NotifyOfferReceivedAsync(
                It.Is<AgentOfferNotification>(o =>
                    o.UserId == "u1" &&
                    o.QueueId == "q1" &&
                    o.ReservationId == "r1" &&
                    o.AutoOpenActivity == expectedAutoOpen),
                It.IsAny<CancellationToken>()),
            Times.Once);

        notifier.Verify(
            n => n.NotifyQueueStatsChangedAsync(
                It.Is<QueueStatsNotification>(q => q.QueueId == "q1" && q.WaitingCount == 2),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueueItemAssigned_BroadcastsOfferRevokedAccepted()
    {
        // Arrange
        var reservationManager = new Mock<IActivityReservationManager>();
        reservationManager.Setup(m => m.FindByIdAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ActivityReservation
            {
                ItemId = "r1",
                AgentId = "a1",
                QueueId = "q1",
                Status = ReservationStatus.Accepted,
            });

        var agentManager = new Mock<IAgentProfileManager>();
        agentManager.Setup(m => m.FindByIdAsync("a1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "a1", UserId = "u1" });

        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore.Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var notifier = new Mock<IContactCenterRealTimeNotifier>();
        var handler = CreateHandler(notifier, reservationManager, agentManager, queueItemStore);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAssigned,
            AggregateId = "r1",
            OccurredUtc = _now,
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        notifier.Verify(
            n => n.NotifyOfferRevokedAsync(
                It.Is<AgentOfferRevokedNotification>(o => o.UserId == "u1" && o.Reason == AgentOfferRevokedReason.Accepted),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_QueueItemAdded_BroadcastsQueueStats()
    {
        // Arrange
        var queueItemStore = new Mock<IQueueItemStore>();
        queueItemStore.Setup(m => m.FindByIdAsync("qi1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new QueueItem { ItemId = "qi1", QueueId = "q1" });
        queueItemStore.Setup(m => m.ListWaitingAsync("q1", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new QueueItem { ItemId = "qi1" }]);

        var notifier = new Mock<IContactCenterRealTimeNotifier>();
        var handler = CreateHandler(notifier, queueItemStore: queueItemStore);

        var interactionEvent = new InteractionEvent
        {
            EventType = ContactCenterConstants.Events.QueueItemAdded,
            AggregateId = "qi1",
            OccurredUtc = _now,
        };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        notifier.Verify(
            n => n.NotifyQueueStatsChangedAsync(
                It.Is<QueueStatsNotification>(q => q.QueueId == "q1" && q.WaitingCount == 1),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static ContactCenterRealTimeEventHandler CreateHandler(
        Mock<IContactCenterRealTimeNotifier> notifier,
        Mock<IActivityReservationManager> reservationManager = null,
        Mock<IAgentProfileManager> agentManager = null,
        Mock<IQueueItemStore> queueItemStore = null,
        Mock<IOmnichannelActivityManager> activityManager = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        var services = new ServiceCollection()
            .AddSingleton((agentManager ?? new Mock<IAgentProfileManager>()).Object)
            .AddSingleton((reservationManager ?? new Mock<IActivityReservationManager>()).Object)
            .AddSingleton((queueItemStore ?? new Mock<IQueueItemStore>()).Object)
            .AddSingleton((activityManager ?? new Mock<IOmnichannelActivityManager>()).Object)
            .AddSingleton(MockUserManager().Object)
            .AddSingleton(MockDisplayNameProvider().Object)
            .BuildServiceProvider();

        return new ContactCenterRealTimeEventHandler(
            notifier.Object,
            services,
            clock.Object);
    }

    private static Mock<IDisplayNameProvider> MockDisplayNameProvider()
    {
        var displayNameProvider = new Mock<IDisplayNameProvider>();
        displayNameProvider
            .Setup(provider => provider.GetAsync(It.IsAny<IUser>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((IUser user, CancellationToken _) => user?.UserName);

        return displayNameProvider;
    }

    private static Mock<UserManager<IUser>> MockUserManager()
    {
        var store = new Mock<IUserStore<IUser>>();

        return new Mock<UserManager<IUser>>(
            store.Object,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null);
    }
}
