using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The automated conversation's hangup, told to the Contact Center once its caller was handed to a queue.
/// </summary>
public sealed class QueuedCallerAbandonmentHandlerTests
{
    private static readonly DateTime _now = new(2026, 9, 24, 4, 10, 35, DateTimeKind.Utc);

    [Fact]
    public async Task CallerAbandonedAsync_WhenTheCallerWasStillWaiting_EndsTheInteractionAndReleasesTheQueue()
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
        }.RestorePersistedStatus(InteractionStatus.Ringing);
        var (handler, interactionManager, synchronization) = CreateHandler(interaction);

        // Act
        await handler.CallerAbandonedAsync("activity-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(_now, interaction.EndedUtc);
        interactionManager.Verify(manager => manager.UpdateAsync(interaction, It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Once);
        synchronization.Verify(service => service.ReconcileEndedOfferAsync("interaction-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CallerAbandonedAsync_WhenTheCallerWasJoinedToAnAgent_LeavesTheCallToItsOwnHangup()
    {
        // Arrange
        // The caller talked to an agent. Their hangup reaches the Contact Center as the call's own event, with the
        // provider's cause, and that is what ends the call, measures its talk time and starts the agent's wrap-up.
        var interaction = new Interaction
        {
            ItemId = "interaction-1",
            ActivityItemId = "activity-1",
            AnsweredUtc = _now.AddSeconds(-45),
        }.RestorePersistedStatus(InteractionStatus.Connected);
        var (handler, interactionManager, synchronization) = CreateHandler(interaction);

        // Act
        await handler.CallerAbandonedAsync("activity-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(InteractionStatus.Connected, interaction.Status);
        Assert.Null(interaction.EndedUtc);
        interactionManager.Verify(manager => manager.UpdateAsync(It.IsAny<Interaction>(), It.IsAny<System.Text.Json.Nodes.JsonNode>(), It.IsAny<CancellationToken>()), Times.Never);
        synchronization.Verify(service => service.ReconcileEndedOfferAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static (QueuedCallerAbandonmentHandler Handler, Mock<IInteractionManager> InteractionManager, Mock<IProviderVoiceOfferSynchronizationService> Synchronization) CreateHandler(Interaction interaction)
    {
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("activity-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(interaction);

        var synchronization = new Mock<IProviderVoiceOfferSynchronizationService>();
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        var handler = new QueuedCallerAbandonmentHandler(
            interactionManager.Object,
            synchronization.Object,
            clock.Object,
            NullLogger<QueuedCallerAbandonmentHandler>.Instance);

        return (handler, interactionManager, synchronization);
    }
}
