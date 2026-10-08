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
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([callback]);
        callbackManager.Setup(m => m.FindByIdAsync(callback.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(callback);

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

    // Live (2026-09-27), one callback became two preview calls a minute apart. The pass read every due callback at once,
    // promoting the first committed the session (enqueuing saves it), and saving the second -- read before that commit, so
    // no longer tracked -- inserted it as a second, still-pending row the next pass promoted again. Each callback is read
    // again on its own before it is claimed, and the fresh copy is the one saved.
    [Fact]
    public async Task PromoteDueAsync_ReadsEachCallbackAgain_AndSavesTheFreshCopy_NotTheOneFromTheBatch()
    {
        // Arrange
        var batchCopy = new CallbackRequest { ItemId = "cb1", Destination = "+15551234567", QueueId = "q1", Status = CallbackRequestStatus.Pending };
        var freshCopy = new CallbackRequest { ItemId = "cb1", Destination = "+15551234567", QueueId = "q1", Status = CallbackRequestStatus.Pending };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([batchCopy]);
        callbackManager.Setup(m => m.FindByIdAsync("cb1", It.IsAny<CancellationToken>())).ReturnsAsync(freshCopy);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var service = CreateService(callbackManager, activityManager, new Mock<IActivityQueueService>(), new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(CallbackRequestStatus.Scheduled, freshCopy.Status);
        Assert.Equal(CallbackRequestStatus.Pending, batchCopy.Status);
        callbackManager.Verify(m => m.UpdateAsync(batchCopy, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        callbackManager.Verify(m => m.UpdateAsync(freshCopy, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.AtLeastOnce);
    }

    // A callback promoted since the batch was read (by the pass before, or another node) is left alone.
    [Fact]
    public async Task PromoteDueAsync_SkipsACallbackThatIsNoLongerPendingWhenReadAgain()
    {
        // Arrange
        var batchCopy = new CallbackRequest { ItemId = "cb1", QueueId = "q1", Status = CallbackRequestStatus.Pending };
        var alreadyPromoted = new CallbackRequest { ItemId = "cb1", QueueId = "q1", Status = CallbackRequestStatus.Scheduled };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([batchCopy]);
        callbackManager.Setup(m => m.FindByIdAsync("cb1", It.IsAny<CancellationToken>())).ReturnsAsync(alreadyPromoted);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = CreateService(callbackManager, activityManager, new Mock<IActivityQueueService>(), new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        activityManager.Verify(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PromoteDueAsync_WithoutQueue_DoesNotEnqueue()
    {
        // Arrange
        var callback = new CallbackRequest { ItemId = "cb1", Destination = "+15551234567", Status = CallbackRequestStatus.Pending };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([callback]);
        callbackManager.Setup(m => m.FindByIdAsync(callback.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(callback);

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

    [Fact]
    public async Task PromoteDueAsync_ClaimsCallbackAndClearsLeaseAfterPromotion()
    {
        // Arrange
        var callback = new CallbackRequest
        {
            ItemId = "cb1",
            Destination = "+15551234567",
            Status = CallbackRequestStatus.Pending,
            ScheduledUtc = _now.AddMinutes(-1),
        };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([callback]);
        callbackManager.Setup(m => m.FindByIdAsync(callback.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(callback);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        activityManager.Setup(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OmnichannelActivity { ItemId = "act-1" });

        var service = CreateService(callbackManager, activityManager, new Mock<IActivityQueueService>(), new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, count);
        Assert.Equal(CallbackRequestStatus.Scheduled, callback.Status);
        Assert.Equal(1, callback.FenceToken);
        Assert.Null(callback.OwnerToken);
        Assert.Null(callback.LeaseExpiresUtc);
    }

    [Fact]
    public async Task PromoteDueAsync_SkipsCallbackHeldByUnexpiredLease()
    {
        // Arrange
        var callback = new CallbackRequest
        {
            ItemId = "cb1",
            Destination = "+15551234567",
            Status = CallbackRequestStatus.Pending,
            ScheduledUtc = _now.AddMinutes(-1),
            OwnerToken = "other-worker",
            LeaseExpiresUtc = _now.AddMinutes(5),
        };

        var callbackManager = new Mock<ICallbackRequestManager>();
        callbackManager.Setup(m => m.GetDueAsync(_now, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([callback]);
        callbackManager.Setup(m => m.FindByIdAsync(callback.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(callback);

        var activityManager = new Mock<IOmnichannelActivityManager>();
        var service = CreateService(callbackManager, activityManager, new Mock<IActivityQueueService>(), new Mock<IContactCenterEventPublisher>());

        // Act
        var count = await service.PromoteDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, count);
        Assert.Equal(CallbackRequestStatus.Pending, callback.Status);
        activityManager.Verify(m => m.NewAsync(It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        callbackManager.Verify(m => m.UpdateAsync(It.IsAny<CallbackRequest>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static CallbackService CreateService(
        Mock<ICallbackRequestManager> callbackManager,
        Mock<IOmnichannelActivityManager> activityManager,
        Mock<IActivityQueueService> queueService,
        Mock<IContactCenterEventPublisher> publisher)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new CallbackService(
            callbackManager.Object,
            activityManager.Object,
            new FakeContactCenterWorkStateService(activityManager.Object),
            queueService.Object,
            publisher.Object,
            clock.Object);
    }
}
