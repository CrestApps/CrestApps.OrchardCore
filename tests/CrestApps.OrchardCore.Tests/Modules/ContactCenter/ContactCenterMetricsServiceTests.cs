using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Handlers;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterMetricsServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task RecordAsync_AppendsAContributionForTheDayAndType()
    {
        // Arrange
        var store = new Mock<IContactCenterMetricStore>();
        var deltaStore = new Mock<IContactCenterMetricDeltaStore>();

        ContactCenterEventMetricDelta appended = null;
        deltaStore.Setup(s => s.CreateAsync(It.IsAny<ContactCenterEventMetricDelta>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterEventMetricDelta, CancellationToken>((delta, _) => appended = delta)
            .Returns(ValueTask.CompletedTask);

        var service = CreateService(store, deltaStore);

        // Act
        await service.RecordAsync("CallEnded", _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(appended);
        Assert.Equal("2026-01-05", appended.DateKey);
        Assert.Equal("CallEnded", appended.EventType);
        Assert.Equal(1, appended.Count);
    }

    [Fact]
    public async Task RecordAsync_NeverReadsOrWritesTheDailyTotal()
    {
        // Arrange
        // Reading the total and writing it back is what makes it a serialization point, so counting must not
        // touch it at all - not even to read it, since a read is what a later write would be racing against.
        var store = new Mock<IContactCenterMetricStore>(MockBehavior.Strict);
        var deltaStore = new Mock<IContactCenterMetricDeltaStore>();
        deltaStore.Setup(s => s.CreateAsync(It.IsAny<ContactCenterEventMetricDelta>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var service = CreateService(store, deltaStore);

        // Act
        await service.RecordAsync("CallEnded", _now, TestContext.Current.CancellationToken);

        // Assert
        store.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task GetSummaryAsync_AggregatesCountsByEventType()
    {
        // Arrange
        var store = new Mock<IContactCenterMetricStore>();
        store.Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ContactCenterEventMetric { EventType = "CallEnded", Count = 3 },
                new ContactCenterEventMetric { EventType = "CallEnded", Count = 2 },
                new ContactCenterEventMetric { EventType = "QueueItemAdded", Count = 7 },
            ]);

        var deltaStore = new Mock<IContactCenterMetricDeltaStore>();
        deltaStore.Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var service = CreateService(store, deltaStore);

        // Act
        var summary = await service.GetSummaryAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, summary["CallEnded"]);
        Assert.Equal(7, summary["QueueItemAdded"]);
    }

    [Fact]
    public async Task GetSummaryAsync_AddsContributionsThatHaveNotBeenFoldedYet()
    {
        // Arrange
        // A contribution that the roller has not reached is still a real event, so a summary that omitted it
        // would report a number behind the traffic it claims to describe.
        var store = new Mock<IContactCenterMetricStore>();
        store.Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ContactCenterEventMetric { EventType = "CallEnded", Count = 3 },
            ]);

        var deltaStore = new Mock<IContactCenterMetricDeltaStore>();
        deltaStore.Setup(s => s.GetByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ContactCenterEventMetricDelta { EventType = "CallEnded", Count = 1 },
                new ContactCenterEventMetricDelta { EventType = "CallEnded", Count = 1 },
                new ContactCenterEventMetricDelta { EventType = "QueueItemAdded", Count = 1 },
            ]);

        var service = CreateService(store, deltaStore);

        // Act
        var summary = await service.GetSummaryAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, summary["CallEnded"]);
        Assert.Equal(1, summary["QueueItemAdded"]);
    }

    [Fact]
    public async Task ProjectionHandler_RecordsEventType()
    {
        // Arrange
        var metricsService = new Mock<IContactCenterMetricsService>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .Setup(service => service.TryBeginAsync("ContactCenter/MetricsProjection/v1", "event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = new ContactCenterMetricsProjectionHandler(metricsService.Object, deduplication.Object);

        // Act
        await handler.HandleAsync(
            new InteractionEvent { ItemId = "event-1", EventType = "OfferAccepted", OccurredUtc = _now },
            TestContext.Current.CancellationToken);

        // Assert
        metricsService.Verify(s => s.RecordAsync("OfferAccepted", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ProjectionHandler_WhenEventReplayed_DoesNotDoubleCount()
    {
        // Arrange
        var metricsService = new Mock<IContactCenterMetricsService>();
        var deduplication = new Mock<IContactCenterEventDeduplicationService>();
        deduplication
            .SetupSequence(service => service.TryBeginAsync("ContactCenter/MetricsProjection/v1", "event-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true)
            .ReturnsAsync(false);
        var handler = new ContactCenterMetricsProjectionHandler(metricsService.Object, deduplication.Object);
        var interactionEvent = new InteractionEvent { ItemId = "event-1", EventType = "OfferAccepted", OccurredUtc = _now };

        // Act
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);
        await handler.HandleAsync(interactionEvent, TestContext.Current.CancellationToken);

        // Assert
        metricsService.Verify(s => s.RecordAsync("OfferAccepted", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ContactCenterMetricsService CreateService(
        Mock<IContactCenterMetricStore> store,
        Mock<IContactCenterMetricDeltaStore> deltaStore = null)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterMetricsService(store.Object, (deltaStore ?? new Mock<IContactCenterMetricDeltaStore>()).Object, clock.Object);
    }
}
