using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// Proves the inbound-voice probe reports an active interaction only for a provider call that resolves to a
/// durable interaction still in a live status, and that it resolves the call through the provider-scoped lookup
/// when a provider name is supplied so two providers cannot collide on the same call identifier.
/// </summary>
public sealed class InboundVoiceInteractionProbeTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task HasActiveInteractionAsync_WithNoProviderCallId_ReturnsFalse(string providerCallId)
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync("Telnyx", providerCallId, TestContext.Current.CancellationToken);

        // Assert - a blank call id never touches the store.
        Assert.False(active);
        interactionManager.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task HasActiveInteractionAsync_WithProviderName_ResolvesThroughTheProviderScopedLookup()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInteraction(InteractionStatus.Connected));

        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync("Telnyx", "call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(active);
        interactionManager.Verify(
            x => x.FindByProviderInteractionIdAsync("Telnyx", "call-1", It.IsAny<CancellationToken>()),
            Times.Once);
        interactionManager.Verify(
            x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HasActiveInteractionAsync_WithoutProviderName_ResolvesThroughTheGlobalLookup()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByProviderInteractionIdAsync("call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInteraction(InteractionStatus.Ringing));

        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync(null, "call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(active);
        interactionManager.Verify(
            x => x.FindByProviderInteractionIdAsync("call-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HasActiveInteractionAsync_WhenNoInteractionExists_ReturnsFalse()
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);

        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync("Telnyx", "call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(active);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended)]
    [InlineData(InteractionStatus.Failed)]
    public async Task HasActiveInteractionAsync_WhenInteractionHasSettled_ReturnsFalse(InteractionStatus status)
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInteraction(status));

        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync("Telnyx", "call-1", TestContext.Current.CancellationToken);

        // Assert - a terminal interaction is not active, so a late provider event cannot revive it.
        Assert.False(active);
    }

    [Theory]
    [InlineData(InteractionStatus.Created)]
    [InlineData(InteractionStatus.Ringing)]
    [InlineData(InteractionStatus.Connected)]
    [InlineData(InteractionStatus.Held)]
    public async Task HasActiveInteractionAsync_WhenInteractionIsLive_ReturnsTrue(InteractionStatus status)
    {
        // Arrange
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(x => x.FindByProviderInteractionIdAsync(It.IsAny<string>(), "call-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateInteraction(status));

        var probe = new InboundVoiceInteractionProbe(interactionManager.Object);

        // Act
        var active = await probe.HasActiveInteractionAsync("Telnyx", "call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(active);
    }

    private static Interaction CreateInteraction(InteractionStatus status)
        => new Interaction { ItemId = "interaction-1" }.RestorePersistedStatus(status);
}
