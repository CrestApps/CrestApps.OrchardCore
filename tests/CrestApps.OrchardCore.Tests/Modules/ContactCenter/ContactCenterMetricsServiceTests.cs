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
    public async Task RecordAsync_WhenMetricMissing_CreatesWithCountOne()
    {
        // Arrange
        var store = new Mock<IContactCenterMetricStore>();
        store.Setup(s => s.FindAsync("2026-01-05", "CallEnded", It.IsAny<CancellationToken>())).ReturnsAsync((ContactCenterEventMetric)null);

        ContactCenterEventMetric created = null;
        store.Setup(s => s.CreateAsync(It.IsAny<ContactCenterEventMetric>(), It.IsAny<CancellationToken>()))
            .Callback<ContactCenterEventMetric, CancellationToken>((m, _) => created = m)
            .Returns(ValueTask.CompletedTask);

        var service = CreateService(store);

        // Act
        await service.RecordAsync("CallEnded", _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.NotNull(created);
        Assert.Equal("2026-01-05", created.DateKey);
        Assert.Equal(1, created.Count);
    }

    [Fact]
    public async Task RecordAsync_WhenMetricExists_IncrementsCount()
    {
        // Arrange
        var existing = new ContactCenterEventMetric { ItemId = "m1", DateKey = "2026-01-05", EventType = "CallEnded", Count = 4 };
        var store = new Mock<IContactCenterMetricStore>();
        store.Setup(s => s.FindAsync("2026-01-05", "CallEnded", It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var service = CreateService(store);

        // Act
        await service.RecordAsync("CallEnded", _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, existing.Count);
        store.Verify(s => s.UpdateAsync(existing, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetSummaryAsync_AggregatesCountsByEventType()
    {
        // Arrange
        var store = new Mock<IContactCenterMetricStore>();
        store.Setup(s => s.ListByDateRangeAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(
            [
                new ContactCenterEventMetric { EventType = "CallEnded", Count = 3 },
                new ContactCenterEventMetric { EventType = "CallEnded", Count = 2 },
                new ContactCenterEventMetric { EventType = "QueueItemAdded", Count = 7 },
            ]);

        var service = CreateService(store);

        // Act
        var summary = await service.GetSummaryAsync(new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(5, summary["CallEnded"]);
        Assert.Equal(7, summary["QueueItemAdded"]);
    }

    [Fact]
    public async Task ProjectionHandler_RecordsEventType()
    {
        // Arrange
        var metricsService = new Mock<IContactCenterMetricsService>();
        var handler = new ContactCenterMetricsProjectionHandler(metricsService.Object);

        // Act
        await handler.HandleAsync(new InteractionEvent { EventType = "OfferAccepted", OccurredUtc = _now }, TestContext.Current.CancellationToken);

        // Assert
        metricsService.Verify(s => s.RecordAsync("OfferAccepted", _now, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static ContactCenterMetricsService CreateService(Mock<IContactCenterMetricStore> store)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(c => c.UtcNow).Returns(_now);

        return new ContactCenterMetricsService(store.Object, clock.Object);
    }
}
