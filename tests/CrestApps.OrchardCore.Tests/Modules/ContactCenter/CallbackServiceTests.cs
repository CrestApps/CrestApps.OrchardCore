using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class CallbackServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ScheduleAsync_SetsPendingStatusAndPublishesEvent()
    {
        // Arrange
        var callbackManager = new Mock<ICallbackRequestManager>();
        var publisher = new Mock<IContactCenterEventPublisher>();
        var service = CreateService(callbackManager, new Mock<IOmnichannelActivityManager>(), new Mock<IActivityQueueService>(), publisher);

        var callback = new CallbackRequest { ItemId = "cb1", Destination = "+15551234567" };

        // Act
        var scheduled = await service.ScheduleAsync(callback, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(CallbackRequestStatus.Pending, scheduled.Status);
        Assert.Equal(_now, scheduled.RequestedUtc);
        callbackManager.Verify(m => m.CreateAsync(callback, It.IsAny<CancellationToken>()), Times.Once);
        publisher.Verify(p => p.PublishAsync(It.Is<InteractionEvent>(e => e.EventType == ContactCenterConstants.Events.CallbackScheduled), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteDueAsync_CreatesActivityAndEnqueuesWhenQueueSet()
    {
        // Arrange
        var callback = new CallbackRequest
        {
            ItemId = "cb1",
            Destination = "+15551234567",
            QueueId = "q1",
            Status = CallbackRequestStatus.Pending,
            ScheduledUtc = _now.AddMinutes(-1),
        };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.ListDueAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([callback]);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var queueService = new Mock<IActivityQueueService>();
        var service = CreateService(callbackManager, activityManager, queueService, new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(CallbackRequestStatus.Scheduled, callback.Status);
        Assert.Equal("act-1", callback.ActivityItemId);
        queueService.Verify(s => s.EnqueueAsync("act-1", "q1", null, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PromoteDueAsync_WithoutQueue_DoesNotEnqueue()
    {
        // Arrange
        var callback = new CallbackRequest { ItemId = "cb1", Destination = "+15551234567", Status = CallbackRequestStatus.Pending };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.ListDueAsync(_now, It.IsAny<CancellationToken>())).ReturnsAsync([callback]);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var queueService = new Mock<IActivityQueueService>();
        var service = CreateService(callbackManager, activityManager, queueService, new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        queueService.Verify(s => s.EnqueueAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<InteractionPriority?>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CallbackService CreateService(
        Mock<ICallbackRequestManager> callbackManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IActivityQueueService> queueService,
        Mock<IContactCenterEventPublisher> publisher)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new CallbackService(callbackManager.Object, activityManager.Object, queueService.Object, publisher.Object, clock.Object);
    }
}
