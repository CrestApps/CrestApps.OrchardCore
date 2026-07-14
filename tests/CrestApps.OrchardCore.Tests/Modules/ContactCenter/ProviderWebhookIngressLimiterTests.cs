using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderWebhookIngressLimiterTests
{
    private static readonly DateTime _now = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task AcquireConcurrencyAsync_WhenPermitIsHeld_RejectsAdditionalRequest()
    {
        // Arrange
        using var limiter = CreateLimiter(concurrencyPermitLimit: 1);
        using var first = await limiter.AcquireConcurrencyAsync(TestContext.Current.CancellationToken);

        // Act
        using var second = await limiter.AcquireConcurrencyAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.IsAcquired);
        Assert.False(second.IsAcquired);
    }

    [Fact]
    public async Task AcquireRateAsync_WhenProviderConsumesBudget_RejectsOnlyThatProvider()
    {
        // Arrange
        using var limiter = CreateLimiter(ratePermitLimit: 1);
        using var first = await limiter.AcquireRateAsync("provider-a", TestContext.Current.CancellationToken);

        // Act
        using var sameProvider = await limiter.AcquireRateAsync("provider-a", TestContext.Current.CancellationToken);
        using var otherProvider = await limiter.AcquireRateAsync("provider-b", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(first.IsAcquired);
        Assert.False(sameProvider.IsAcquired);
        Assert.NotNull(sameProvider.RetryAfter);
        Assert.True(otherProvider.IsAcquired);
    }

    [Theory]
    [InlineData(0, 120, 60)]
    [InlineData(8, 0, 60)]
    [InlineData(8, 120, 0)]
    public void Constructor_WhenLimitIsInvalid_ThrowsOptionsValidationException(
        int concurrencyPermitLimit,
        int ratePermitLimit,
        int ratePeriodSeconds)
    {
        // Arrange
        var options = Options.Create(new ProviderWebhookIngressOptions
        {
            ConcurrencyPermitLimit = concurrencyPermitLimit,
            RatePermitLimit = ratePermitLimit,
            RatePeriodSeconds = ratePeriodSeconds,
        });

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => new ProviderWebhookIngressLimiter(options, CreateClock()));
    }

    [Theory]
    [InlineData(0, 120)]
    [InlineData(86_401, 120)]
    [InlineData(900, -1)]
    [InlineData(900, 3_601)]
    public void Constructor_WhenFreshnessWindowIsInvalid_ThrowsOptionsValidationException(
        int maximumDeliveryAgeSeconds,
        int maximumFutureSkewSeconds)
    {
        // Arrange
        var options = Options.Create(new ProviderWebhookIngressOptions
        {
            MaximumDeliveryAgeSeconds = maximumDeliveryAgeSeconds,
            MaximumFutureSkewSeconds = maximumFutureSkewSeconds,
        });

        // Act & Assert
        Assert.Throws<OptionsValidationException>(() => new ProviderWebhookIngressLimiter(options, CreateClock()));
    }

    [Theory]
    [InlineData(-900, true)]
    [InlineData(120, true)]
    [InlineData(-901, false)]
    [InlineData(121, false)]
    public void IsFresh_WhenTimestampIsInsideConfiguredWindow_ReturnsExpectedResult(
        int offsetSeconds,
        bool expected)
    {
        // Arrange
        using var limiter = CreateLimiter();

        // Act
        var result = limiter.IsFresh(_now.AddSeconds(offsetSeconds));

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void IsFresh_WhenTimestampIsMissing_ReturnsFalse()
    {
        // Arrange
        using var limiter = CreateLimiter();

        // Act
        var result = limiter.IsFresh(null);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsFresh_WhenTimestampIsNotUtc_ReturnsFalse()
    {
        // Arrange
        using var limiter = CreateLimiter();
        var localTimestamp = DateTime.SpecifyKind(_now, DateTimeKind.Local);

        // Act
        var result = limiter.IsFresh(localTimestamp);

        // Assert
        Assert.False(result);
    }

    private static ProviderWebhookIngressLimiter CreateLimiter(
        int concurrencyPermitLimit = 8,
        int ratePermitLimit = 120)
    {
        return new ProviderWebhookIngressLimiter(
            Options.Create(new ProviderWebhookIngressOptions
            {
                ConcurrencyPermitLimit = concurrencyPermitLimit,
                RatePermitLimit = ratePermitLimit,
            }),
            CreateClock());
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
    }
}
