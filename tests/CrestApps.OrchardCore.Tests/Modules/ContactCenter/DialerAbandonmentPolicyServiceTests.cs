using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class DialerAbandonmentPolicyServiceTests
{
    [Fact]
    public async Task EvaluateAsync_WhenCapNotEnforced_Permits()
    {
        // Arrange
        var service = CreateService();
        var profile = Profile(enforce: false, mode: DialerMode.Power);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(evaluation.IsPermitted);
        Assert.True(evaluation.StatisticsAvailable);
    }

    [Theory]
    [InlineData(DialerMode.Manual)]
    [InlineData(DialerMode.Preview)]
    public async Task EvaluateAsync_WhenEnforcedButNotAutomatedMode_Permits(DialerMode mode)
    {
        // Arrange
        var provider = new Mock<IDialerAbandonmentStatisticsProvider>();
        var service = CreateService(provider.Object);
        var profile = Profile(enforce: true, mode: mode);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(evaluation.IsPermitted);
        provider.Verify(
            p => p.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task EvaluateAsync_WhenEnforcedAutomatedAndStatisticsUnavailable_FailsClosed()
    {
        // Arrange
        var service = CreateService();
        var profile = Profile(enforce: true, mode: DialerMode.Power);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(evaluation.IsPermitted);
        Assert.False(evaluation.StatisticsAvailable);
    }

    [Fact]
    public async Task EvaluateAsync_WhenSampleBelowFloor_Permits()
    {
        // Arrange
        var provider = StatisticsProvider(new DialerAbandonmentStatistics { LiveAnswers = 5, AbandonedCalls = 5 });
        var service = CreateService(provider);
        var profile = Profile(enforce: true, mode: DialerMode.Power, sampleFloor: 30);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(evaluation.IsPermitted);
        Assert.Equal(5, evaluation.SampleSize);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRateExceedsCap_Suppresses()
    {
        // Arrange
        var provider = StatisticsProvider(new DialerAbandonmentStatistics { LiveAnswers = 100, AbandonedCalls = 5 });
        var service = CreateService(provider);
        var profile = Profile(enforce: true, mode: DialerMode.Power, cap: 3, sampleFloor: 30);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(evaluation.IsPermitted);
        Assert.True(evaluation.StatisticsAvailable);
        Assert.Equal(5, evaluation.RatePercent);
    }

    [Fact]
    public async Task EvaluateAsync_WhenRateWithinCap_Permits()
    {
        // Arrange
        var provider = StatisticsProvider(new DialerAbandonmentStatistics { LiveAnswers = 100, AbandonedCalls = 2 });
        var service = CreateService(provider);
        var profile = Profile(enforce: true, mode: DialerMode.Progressive, cap: 3, sampleFloor: 30);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(evaluation.IsPermitted);
        Assert.Equal(2, evaluation.RatePercent);
    }

    [Fact]
    public async Task EvaluateAsync_WhenFirstProviderReturnsNull_UsesNextProvider()
    {
        // Arrange
        var empty = new Mock<IDialerAbandonmentStatisticsProvider>();
        empty
            .Setup(p => p.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((DialerAbandonmentStatistics)null);
        var populated = StatisticsProvider(new DialerAbandonmentStatistics { LiveAnswers = 100, AbandonedCalls = 10 });
        var service = CreateService(empty.Object, populated);
        var profile = Profile(enforce: true, mode: DialerMode.Power, cap: 3, sampleFloor: 30);

        // Act
        var evaluation = await service.EvaluateAsync(profile, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(evaluation.IsPermitted);
        Assert.Equal(10, evaluation.RatePercent);
    }

    private static IDialerAbandonmentStatisticsProvider StatisticsProvider(DialerAbandonmentStatistics statistics)
    {
        var provider = new Mock<IDialerAbandonmentStatisticsProvider>();
        provider
            .Setup(p => p.GetStatisticsAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(statistics);

        return provider.Object;
    }

    private static DefaultDialerAbandonmentPolicyService CreateService(params IDialerAbandonmentStatisticsProvider[] providers)
    {
        var options = Options.Create(new ContactCenterComplianceOptions { AbandonmentRollingWindowMinutes = 30 });

        return new DefaultDialerAbandonmentPolicyService(providers, options);
    }

    private static DialerProfile Profile(
        bool enforce,
        DialerMode mode,
        double cap = 3,
        int sampleFloor = 30)
    {
        return new DialerProfile
        {
            ItemId = "profile1",
            Name = "Test",
            Mode = mode,
            EnforceAbandonmentCap = enforce,
            MaxAbandonmentRatePercent = cap,
            AbandonmentSampleFloor = sampleFloor,
        };
    }
}
