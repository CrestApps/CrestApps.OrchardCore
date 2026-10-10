using System.Text.Json.Nodes;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class QueuedDialerWorkGateTests
{
    private static readonly DateTime _now = new(2026, 10, 9, 15, 0, 0, DateTimeKind.Utc);

    // Turning a dialer profile off used to stop only the pacer: Preview and Manual records loaded under it were still
    // offered to every available agent, because routing never looked at the profile. A turned-off profile places no
    // calls, so its records are held at the back of the queue before any agent is reserved for them.
    [Fact]
    public async Task TryHoldBackAsync_WhenTheDialerProfileIsTurnedOff_HoldsTheRecordBack()
    {
        // Arrange
        var harness = new Harness(CreateProfile(enabled: false));
        var queueItem = CreateQueueItem();

        // Act
        var heldBack = await harness.CreateGate().TryHoldBackAsync(queueItem, _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(heldBack);
        Assert.Equal(_now, queueItem.EnqueuedUtc);
        Assert.Equal(QueueItemStatus.Waiting, queueItem.Status);
        harness.QueueItemManager.Verify(
            manager => manager.UpdateAsync(queueItem, It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // A turned-off profile must not act on its records either: one with no attempts left stays queued, untouched, rather
    // than being taken out and completed by the dialer.
    [Fact]
    public async Task TryHoldBackAsync_WhenTheDialerProfileIsTurnedOffAndTheRecordIsOutOfAttempts_DoesNotTakeItOut()
    {
        // Arrange
        var profile = CreateProfile(enabled: false);
        profile.MaxAttempts = 1;

        var harness = new Harness(profile, activityAttempts: 2);
        var queueItem = CreateQueueItem();

        // Act
        var heldBack = await harness.CreateGate().TryHoldBackAsync(queueItem, _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(heldBack);
        harness.QueueService.Verify(
            service => service.DequeueAsync(It.IsAny<QueueItem>(), It.IsAny<QueueItemStatus>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task TryHoldBackAsync_WhenTheDialerProfileIsOnAndTheRecordIsDue_LetsRoutingOfferIt()
    {
        // Arrange
        var harness = new Harness(CreateProfile(enabled: true));
        var queueItem = CreateQueueItem();

        // Act
        var heldBack = await harness.CreateGate().TryHoldBackAsync(queueItem, _now, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(heldBack);
        harness.QueueItemManager.Verify(
            manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static DialerProfile CreateProfile(bool enabled)
    {
        return new DialerProfile
        {
            ItemId = "profile-1",
            Name = "Preview profile",
            Mode = DialerMode.Preview,
            Enabled = enabled,
        };
    }

    private static QueueItem CreateQueueItem()
    {
        return new QueueItem
        {
            ItemId = "item-1",
            QueueId = "__campaign-queue__campaign-1",
            ActivityItemId = "act-1",
            DialerProfileId = "profile-1",
            EnqueuedUtc = _now.AddMinutes(-30),
        }.RestorePersistedStatus(QueueItemStatus.Waiting);
    }

    private sealed class Harness
    {
        private readonly Mock<IDialerProfileReader> _profileReader = new();
        private readonly Mock<IOmnichannelActivityManager> _activityManager = new();
        private readonly Mock<IInteractionManager> _interactionManager = new();
        private readonly Mock<IContactCenterWorkStateService> _workStateService = new();

        public Harness(DialerProfile profile, int activityAttempts = 0)
        {
            _profileReader
                .Setup(reader => reader.FindByIdAsync(profile.ItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(profile);

            _activityManager
                .Setup(manager => manager.FindByIdAsync("act-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(new OmnichannelActivity
                {
                    ItemId = "act-1",
                    Source = ActivitySources.PreviewDial,
                    Status = ActivityStatus.NotStated,
                    Attempts = activityAttempts,
                });

            QueueItemManager
                .Setup(manager => manager.UpdateAsync(It.IsAny<QueueItem>(), It.IsAny<JsonNode>(), It.IsAny<CancellationToken>()))
                .Returns(ValueTask.CompletedTask);
        }

        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IActivityQueueService> QueueService { get; } = new();

        public QueuedDialerWorkGate CreateGate()
        {
            return new QueuedDialerWorkGate(
                _profileReader.Object,
                _activityManager.Object,
                _interactionManager.Object,
                _workStateService.Object,
                QueueItemManager.Object,
                QueueService.Object,
                Mock.Of<IContactCenterScopeExecutor>(),
                NullLogger<QueuedDialerWorkGate>.Instance);
        }
    }
}
