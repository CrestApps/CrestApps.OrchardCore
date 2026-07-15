using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceWebhookEndpointTests : IDisposable
{
    private readonly ProviderWebhookIngressLimiter _ingressLimiter = CreateIngressLimiter();
    private readonly TestContactCenterFeatureWorkManager _workManager = new();

    [Fact]
    public async Task HandleAsync_WhenPayloadExceedsLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentLength = ProviderVoiceWebhookEndpoint.MaximumRequestBodySizeBytes + 1;

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, _ingressLimiter, _workManager, httpContext);

        // Assert
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusResult.StatusCode);
        processor.Verify(
            value => value.ProcessAsync(It.IsAny<ProviderVoiceWebhookRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenServerRejectsChunkedPayload_ReturnsPayloadTooLarge()
    {
        // Arrange
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new PayloadTooLargeStream();

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, _ingressLimiter, _workManager, httpContext);

        // Assert
        var statusResult = Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, statusResult.StatusCode);
        processor.Verify(
            value => value.ProcessAsync(It.IsAny<ProviderVoiceWebhookRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenPayloadIsAccepted_DoesNotPassRequestCancellationToProcessing()
    {
        // Arrange
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        processor
            .Setup(value => value.ProcessAsync(It.IsAny<ProviderVoiceWebhookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderVoiceWebhookOutcome
            {
                Status = ProviderVoiceWebhookStatus.Accepted,
            });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream("{}"u8.ToArray());
        httpContext.RequestAborted = new CancellationTokenSource().Token;

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, _ingressLimiter, _workManager, httpContext);

        // Assert
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        processor.Verify(
            value => value.ProcessAsync(
                It.IsAny<ProviderVoiceWebhookRequest>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }

    [Fact]
    public async Task HandleAsync_WhenConcurrencyLimitIsExhausted_ReturnsTooManyRequests()
    {
        // Arrange
        using var limiter = CreateIngressLimiter(concurrencyPermitLimit: 1);
        using var heldLease = await limiter.AcquireConcurrencyAsync(TestContext.Current.CancellationToken);
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream("{}"u8.ToArray());

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, limiter, _workManager, httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        processor.Verify(
            value => value.ProcessAsync(It.IsAny<ProviderVoiceWebhookRequest>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HandleAsync_WhenAuthenticatedProviderIsRateLimited_ReturnsRetryAfter()
    {
        // Arrange
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        processor
            .Setup(value => value.ProcessAsync(It.IsAny<ProviderVoiceWebhookRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderVoiceWebhookOutcome
            {
                Status = ProviderVoiceWebhookStatus.RateLimited,
                RetryAfter = TimeSpan.FromSeconds(12),
            });
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream("{}"u8.ToArray());

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, _ingressLimiter, _workManager, httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.Equal("12", httpContext.Response.Headers.RetryAfter);
    }

    public void Dispose()
    {
        _ingressLimiter.Dispose();
    }

    private static ProviderWebhookIngressLimiter CreateIngressLimiter(int concurrencyPermitLimit = 8)
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc));

        return new ProviderWebhookIngressLimiter(
            Options.Create(new ProviderWebhookIngressOptions
            {
                ConcurrencyPermitLimit = concurrencyPermitLimit,
            }),
            clock.Object);
    }
}
