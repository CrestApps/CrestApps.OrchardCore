using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Endpoints;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookEndpointTests : IDisposable
{
    private const string Payload = "{\"call_id\":\"c1\",\"state\":\"ringing\"}";
    private readonly ProviderWebhookIngressLimiter _ingressLimiter = CreateIngressLimiter();

    [Fact]
    public async Task Call_WhenSigningSecretIsMissing_RejectsWebhook()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var httpContext = CreateHttpContext();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            _ingressLimiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = null,
            }),
            new EphemeralDataProtectionProvider(),
            NullLogger<DialPadContactCenterStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenSigningSecretCannotBeUnprotected_FailsClosed()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            _ingressLimiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = "not-a-protected-secret",
            }),
            new EphemeralDataProtectionProvider(),
            NullLogger<DialPadContactCenterStartup>.Instance,
            CreateHttpContext());

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenPayloadExceedsLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var httpContext = CreateHttpContext();
        httpContext.Request.ContentLength = DialPadWebhookEndpoint.MaximumRequestBodySizeBytes + 1;
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            _ingressLimiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = null,
            }),
            new EphemeralDataProtectionProvider(),
            NullLogger<DialPadContactCenterStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenServerRejectsChunkedPayload_ReturnsPayloadTooLarge()
    {
        // Arrange
        var webhookService = new Mock<IDialPadWebhookService>();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new PayloadTooLargeStream();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            _ingressLimiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
            }),
            new EphemeralDataProtectionProvider(),
            NullLogger<DialPadContactCenterStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenPayloadIsValid_DoesNotPassRequestCancellationToProcessing()
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var webhookService = new Mock<IDialPadWebhookService>();
        webhookService
            .Setup(service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialPadWebhookResult.Updated);
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(Payload, secret)));
        httpContext.RequestAborted = new CancellationTokenSource().Token;
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            _ingressLimiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = protectedSecret,
            }),
            dataProtectionProvider,
            NullLogger<DialPadContactCenterStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(
                It.IsAny<DialPadCallEvent>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }

    [Fact]
    public async Task Call_WhenAuthenticatedProviderExceedsRateLimit_ReturnsTooManyRequests()
    {
        // Arrange
        const string secret = "shhh";
        using var limiter = CreateIngressLimiter(ratePermitLimit: 1);
        using var consumedLease = await limiter.AcquireRateAsync(DialPadConstants.ProviderTechnicalName, TestContext.Current.CancellationToken);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var webhookService = new Mock<IDialPadWebhookService>();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(Payload, secret)));

        // Act
        var result = await DialPadWebhookEndpoint.HandleAsync(
            webhookService.Object,
            limiter,
            SiteServiceFactory.Create(new DialPadSettings
            {
                IsEnabled = true,
                WebhookSigningSecret = protectedSecret,
            }),
            dataProtectionProvider,
            NullLogger<DialPadContactCenterStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status429TooManyRequests, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.False(string.IsNullOrEmpty(httpContext.Response.Headers.RetryAfter));
        webhookService.Verify(
            service => service.ProcessAsync(It.IsAny<DialPadCallEvent>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    public void Dispose()
    {
        _ingressLimiter.Dispose();
    }

    private static DefaultHttpContext CreateHttpContext()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream("signed-payload"u8.ToArray());

        return httpContext;
    }

    private static string CreateJwt(string payloadJson, string secret)
    {
        var header = Base64Url(Encoding.UTF8.GetBytes("{\"alg\":\"HS256\",\"typ\":\"JWT\"}"));
        var payload = Base64Url(Encoding.UTF8.GetBytes(payloadJson));
        var signingInput = $"{header}.{payload}";
        var signature = Base64Url(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signingInput)));

        return $"{signingInput}.{signature}";
    }

    private static string Base64Url(byte[] bytes)
    {
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static ProviderWebhookIngressLimiter CreateIngressLimiter(int ratePermitLimit = 120)
    {
        return new ProviderWebhookIngressLimiter(Options.Create(new ProviderWebhookIngressOptions
        {
            RatePermitLimit = ratePermitLimit,
        }));
    }
}
