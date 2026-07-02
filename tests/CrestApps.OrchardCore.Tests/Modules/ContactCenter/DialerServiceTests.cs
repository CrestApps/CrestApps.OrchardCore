using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Logging;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerServiceTests
{
    [Theory]
    [InlineData(DialerMode.Manual)]
    [InlineData(DialerMode.Preview)]
    public async Task RunCycleAsync_WhenModeIsAgentDriven_ReturnsZeroWithoutResolvingStrategy(DialerMode mode)
    {
        // Arrange
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        var resolver = new Mock<IDialerStrategyResolver>();
        var service = CreateService(voiceCallRouter, resolver);

        // Act
        var started = await service.RunCycleAsync(CreateProfile(mode), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
        resolver.Verify(r => r.Resolve(It.IsAny<DialerMode>()), Times.Never);
    }

    [Fact]
    public async Task RunCycleAsync_WhenProviderCannotRouteOutbound_ReturnsZero()
    {
        // Arrange
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        voiceCallRouter.Setup(r => r.CanRouteOutbound(It.IsAny<string>())).Returns(false);
        var resolver = new Mock<IDialerStrategyResolver>();
        var service = CreateService(voiceCallRouter, resolver);

        // Act
        var started = await service.RunCycleAsync(CreateProfile(DialerMode.Power), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
        resolver.Verify(r => r.Resolve(It.IsAny<DialerMode>()), Times.Never);
    }

    [Fact]
    public async Task RunCycleAsync_WhenNoStrategyForMode_ReturnsZero()
    {
        // Arrange
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        voiceCallRouter.Setup(r => r.CanRouteOutbound(It.IsAny<string>())).Returns(true);

        var resolver = new Mock<IDialerStrategyResolver>();
        resolver.Setup(r => r.Resolve(DialerMode.Predictive)).Returns((IDialerStrategy)null);

        var service = CreateService(voiceCallRouter, resolver);

        // Act
        var started = await service.RunCycleAsync(CreateProfile(DialerMode.Predictive), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(0, started);
    }

    [Fact]
    public async Task RunCycleAsync_WhenStrategyResolved_DelegatesToStrategy()
    {
        // Arrange
        var voiceCallRouter = new Mock<IVoiceContactCenterCallRouter>();
        voiceCallRouter.Setup(r => r.CanRouteOutbound(It.IsAny<string>())).Returns(true);

        var profile = CreateProfile(DialerMode.Progressive);

        var strategy = new Mock<IDialerStrategy>();
        strategy
            .Setup(s => s.RunCycleAsync(profile, It.IsAny<CancellationToken>()))
            .ReturnsAsync(4);

        var resolver = new Mock<IDialerStrategyResolver>();
        resolver.Setup(r => r.Resolve(DialerMode.Progressive)).Returns(strategy.Object);

        var service = CreateService(voiceCallRouter, resolver);

        // Act
        var started = await service.RunCycleAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(4, started);
        strategy.Verify(s => s.RunCycleAsync(profile, It.IsAny<CancellationToken>()), Times.Once);
    }

    private static DialerProfile CreateProfile(DialerMode mode)
    {
        return new DialerProfile
        {
            ItemId = "profile1",
            Name = "Test",
            QueueId = "q1",
            ProviderName = "test",
            Mode = mode,
            Enabled = true,
        };
    }

    private static DialerService CreateService(
        Mock<IVoiceContactCenterCallRouter> voiceCallRouter,
        Mock<IDialerStrategyResolver> resolver)
    {
        return new DialerService(
            voiceCallRouter.Object,
            resolver.Object,
            new Mock<ILogger<DialerService>>().Object);
    }
}
