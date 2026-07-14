using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.DialPad;
using CrestApps.OrchardCore.DialPad.Endpoints;
using CrestApps.OrchardCore.DialPad.Models;
using CrestApps.OrchardCore.DialPad.Services;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.DialPad;

public sealed class DialPadWebhookEndpointTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string _payload = $"{{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":{new DateTimeOffset(_now).ToUnixTimeMilliseconds()}}}";
    private readonly ProviderWebhookIngressLimiter _ingressLimiter = CreateIngressLimiter();

    [Fact]
    public async Task Call_WhenSigningSecretIsMissing_RejectsWebhook()
    {
        // Arrange
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenSigningSecretCannotBeUnprotected_FailsClosed()
    {
        // Arrange
        var inbox = CreateInbox();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenPayloadExceedsLimit_ReturnsPayloadTooLarge()
    {
        // Arrange
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        httpContext.Request.ContentLength = DialPadWebhookEndpoint.MaximumRequestBodySizeBytes + 1;
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenServerRejectsChunkedPayload_ReturnsPayloadTooLarge()
    {
        // Arrange
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new PayloadTooLargeStream();
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
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
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));
        httpContext.RequestAborted = new CancellationTokenSource().Token;
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
        inbox.Verify(
            service => service.DispatchAsync(
                "message-1",
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }

    [Fact]
    public async Task Call_WhenDurableInboxIsBusy_ReturnsServiceUnavailableWithoutDispatching()
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        inbox
            .Setup(service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            });
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));

        // Act
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        Assert.Equal(
            StatusCodes.Status503ServiceUnavailable,
            Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        inbox.Verify(
            service => service.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
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
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));

        // Act
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(-901)]
    [InlineData(121)]
    public async Task Call_WhenSignedTimestampIsOutsideFreshnessWindow_ReturnsBadRequest(int offsetSeconds)
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var timestamp = new DateTimeOffset(_now.AddSeconds(offsetSeconds)).ToUnixTimeMilliseconds();
        var payload = $"{{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":{timestamp}}}";
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(payload, secret)));

        // Act
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData("{\"call_id\":\"c1\",\"state\":\"ringing\"}")]
    [InlineData("{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":\"invalid\"}")]
    [InlineData("{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":9223372036854775807}")]
    public async Task Call_WhenSignedTimestampIsMissingMalformedOrOutOfRange_ReturnsBadRequest(string payload)
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialPadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(payload, secret)));

        // Act
        var result = await DialPadWebhookEndpoint.HandleAsync(
            inbox.Object,
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
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
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
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return new ProviderWebhookIngressLimiter(
            Options.Create(new ProviderWebhookIngressOptions
            {
                RatePermitLimit = ratePermitLimit,
            }),
            clock.Object);
    }

    private static Mock<IProviderWebhookInbox> CreateInbox()
    {
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(service => service.AcceptAsync(
                It.IsAny<ProviderWebhookInboxDelivery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
                MessageId = "message-1",
            });
        inbox
            .Setup(service => service.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        return inbox;
    }
}
