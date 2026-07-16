using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ContactCenterRetentionServiceTests
{
    private static readonly DateTime _cutoff = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PurgeInteractionEventsAsync_DeletesEveryExpiredEvent()
    {
        // Arrange
        var expired = new[]
        {
            new InteractionEvent { ItemId = "e1" },
            new InteractionEvent { ItemId = "e2" },
        };

        var store = new Mock<IInteractionEventStore>();
        store.Setup(s => s.ListOlderThanAsync(_cutoff, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync(expired);

        var service = new ContactCenterRetentionService(store.Object);

        // Act
        var purged = await service.PurgeInteractionEventsAsync(_cutoff, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(2, purged);
        store.Verify(s => s.DeleteAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PurgeInteractionEventsAsync_WhenNoExpiredEvents_DeletesNothing()
    {
        // Arrange
        var store = new Mock<IInteractionEventStore>();
        store.Setup(s => s.ListOlderThanAsync(_cutoff, It.IsAny<int>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var service = new ContactCenterRetentionService(store.Object);

        // Act
        var purged = await service.PurgeInteractionEventsAsync(_cutoff, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, purged);
        store.Verify(s => s.DeleteAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
