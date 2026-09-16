using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using YesSql;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the direct-to-agent hold sweep bounds how long a personal-line caller waits: once the entry point's
/// ring window elapses the caller is sent to voicemail, while an entry point that disabled voicemail (ring window
/// zero) keeps the call held and re-offers it to the named agent instead of ever giving up.
/// </summary>
public sealed class DirectHoldTimeoutServiceTests
{
    private static readonly DateTime _now = new(2026, 1, 5, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task ProcessDueAsync_WhenNothingIsWaiting_ReturnsZero_AndResolvesNoInteractions()
    {
        // Arrange
        var harness = new Harness();
        harness.QueueItemManager
            .Setup(x => x.GetWaitingAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, handled);
        harness.InteractionManager.Verify(
            x => x.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenTheRingWindowHasNotElapsed_LeavesTheCallHeld()
    {
        // Arrange - the call was enqueued now with a 30s window, so it is still ringing.
        var harness = new Harness();
        harness.SetWaiting(CreateWaitingItem(enqueuedUtc: _now));
        harness.SetInteraction(CreateInteraction(ringTimeoutSeconds: 30));

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert - nothing is timed out to voicemail while the window is still open.
        Assert.Equal(0, handled);
        harness.Processor.Verify(
            x => x.TimeoutDirectHoldAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenTheRingWindowElapsedAndVoicemailIsEnabled_TimesOutToVoicemail()
    {
        // Arrange - enqueued 60s ago with a 30s window: the window has elapsed.
        var harness = new Harness();
        harness.SetWaiting(CreateWaitingItem(enqueuedUtc: _now.AddSeconds(-60)));
        harness.SetInteraction(CreateInteraction(ringTimeoutSeconds: 30));
        harness.Processor
            .Setup(x => x.TimeoutDirectHoldAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, handled);
        harness.Processor.Verify(x => x.TimeoutDirectHoldAsync("activity-1", It.IsAny<CancellationToken>()), Times.Once);
        harness.Session.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        harness.InboundVoiceService.Verify(
            x => x.OfferToAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenVoicemailIsDisabled_ReOffersToTheNamedAgent()
    {
        // Arrange - ring window zero means voicemail is disabled, so the held call is re-offered to its agent.
        var harness = new Harness();
        harness.SetWaiting(CreateWaitingItem(enqueuedUtc: _now.AddSeconds(-300)));
        harness.SetInteraction(CreateInteraction(ringTimeoutSeconds: 0, targetAgentId: "agent-9"));
        harness.InboundVoiceService
            .Setup(x => x.OfferToAgentAsync("activity-1", ContactCenterConstants.DirectRouting.QueueId, "agent-9", 0, It.IsAny<CancellationToken>()))
            .ReturnsAsync("user-9");

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, handled);
        harness.InboundVoiceService.Verify(
            x => x.OfferToAgentAsync("activity-1", ContactCenterConstants.DirectRouting.QueueId, "agent-9", 0, It.IsAny<CancellationToken>()),
            Times.Once);
        harness.Processor.Verify(
            x => x.TimeoutDirectHoldAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenVoicemailIsDisabledButNoTargetAgentIsRecorded_LeavesTheCallHeld()
    {
        // Arrange
        var harness = new Harness();
        harness.SetWaiting(CreateWaitingItem(enqueuedUtc: _now.AddSeconds(-300)));
        harness.SetInteraction(CreateInteraction(ringTimeoutSeconds: 0, targetAgentId: null));

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert - without a named agent there is nobody to re-offer to, so the call is left for the next sweep.
        Assert.Equal(0, handled);
        harness.InboundVoiceService.Verify(
            x => x.OfferToAgentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessDueAsync_WhenTheInteractionIsMissing_SkipsTheItem()
    {
        // Arrange
        var harness = new Harness();
        harness.SetWaiting(CreateWaitingItem(enqueuedUtc: _now.AddSeconds(-300)));
        harness.InteractionManager
            .Setup(x => x.FindByActivityIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        // Act
        var handled = await harness.CreateService().ProcessDueAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, handled);
        harness.Processor.Verify(
            x => x.TimeoutDirectHoldAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    private static QueueItem CreateWaitingItem(DateTime enqueuedUtc)
        => new()
        {
            ItemId = "queue-1",
            QueueId = ContactCenterConstants.DirectRouting.QueueId,
            ActivityItemId = "activity-1",
            EnqueuedUtc = enqueuedUtc,
        };

    private static Interaction CreateInteraction(int ringTimeoutSeconds, string targetAgentId = null)
    {
        var interaction = new Interaction { ItemId = "interaction-1", ActivityItemId = "activity-1" };

        interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.RingTimeoutMetadataKey] = ringTimeoutSeconds.ToString();

        if (targetAgentId is not null)
        {
            interaction.TechnicalMetadata[ContactCenterConstants.DirectRouting.TargetAgentMetadataKey] = targetAgentId;
        }

        return interaction;
    }

    private sealed class Harness
    {
        public Mock<IQueueItemManager> QueueItemManager { get; } = new();

        public Mock<IInteractionManager> InteractionManager { get; } = new();

        public Mock<IInboundVoiceCallProcessor> Processor { get; } = new();

        public Mock<IInboundVoiceService> InboundVoiceService { get; } = new();

        public Mock<ISession> Session { get; } = new();

        public void SetWaiting(params QueueItem[] items)
            => QueueItemManager
                .Setup(x => x.GetWaitingAsync(ContactCenterConstants.DirectRouting.QueueId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(items);

        public void SetInteraction(Interaction interaction)
            => InteractionManager
                .Setup(x => x.FindByActivityIdAsync(interaction.ActivityItemId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(interaction);

        public DirectHoldTimeoutService CreateService()
        {
            var clock = new Mock<IClock>();
            clock.SetupGet(x => x.UtcNow).Returns(_now);

            return new DirectHoldTimeoutService(
                QueueItemManager.Object,
                InteractionManager.Object,
                Processor.Object,
                InboundVoiceService.Object,
                Session.Object,
                clock.Object,
                NullLogger<DirectHoldTimeoutService>.Instance);
        }
    }
}
