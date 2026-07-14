using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using Microsoft.Extensions.Options;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderWebhookIngressLimiterTests
{
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
        Assert.Throws<OptionsValidationException>(() => new ProviderWebhookIngressLimiter(options));
    }

    private static ProviderWebhookIngressLimiter CreateLimiter(
        int concurrencyPermitLimit = 8,
        int ratePermitLimit = 120)
    {
        return new ProviderWebhookIngressLimiter(Options.Create(new ProviderWebhookIngressOptions
        {
            ConcurrencyPermitLimit = concurrencyPermitLimit,
            RatePermitLimit = ratePermitLimit,
        }));
    }
}
