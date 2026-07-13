using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Endpoints;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.AspNetCore.Http;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

public sealed class ProviderVoiceWebhookEndpointTests
{
    [Fact]
    public async Task HandleAsync_WhenPayloadExceedsLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        var processor = new Mock<IProviderVoiceWebhookProcessor>();
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentLength = ProviderVoiceWebhookEndpoint.MaximumRequestBodySizeBytes + 1;

        // Act
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, httpContext);

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
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, httpContext);

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
        var result = await ProviderVoiceWebhookEndpoint.HandleAsync("provider", processor.Object, httpContext);

        // Assert
        Assert.IsAssignableFrom<IStatusCodeHttpResult>(result);
        processor.Verify(
            value => value.ProcessAsync(
                It.IsAny<ProviderVoiceWebhookRequest>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }
}
