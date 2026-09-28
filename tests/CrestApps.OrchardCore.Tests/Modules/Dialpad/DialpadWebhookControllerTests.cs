using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Dialpad;
using CrestApps.OrchardCore.Dialpad.Endpoints;
using CrestApps.OrchardCore.Dialpad.Models;
using CrestApps.OrchardCore.Dialpad.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;
using DialpadStartup = CrestApps.OrchardCore.Dialpad.Startup;

namespace CrestApps.OrchardCore.Tests.Modules.Dialpad;

public sealed class DialpadWebhookEndpointTests : IDisposable
{
    private static readonly DateTime _now = new(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
    private static readonly string _payload = $"{{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":{new DateTimeOffset(_now).ToUnixTimeMilliseconds()}}}";
    private readonly ProviderWebhookIngressLimiter _ingressLimiter = CreateIngressLimiter();

    [Fact]
    public async Task Call_WhenSigningSecretIsMissing_RejectsWebhook()
    {
        // Arrange
        var inbox = CreateInbox();
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = null,
                },
            }),
            new EphemeralDataProtectionProvider(),
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
        var webhookService = CreateWebhookService();
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = "not-a-protected-secret",
                },
            }),
            new EphemeralDataProtectionProvider(),
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            CreateHttpContext(inbox.Object, _ingressLimiter));

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
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.ContentLength = DialpadWebhookEndpoint.MaximumRequestBodySizeBytes + 1;
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = null,
                },
            }),
            new EphemeralDataProtectionProvider(),
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.Body = new PayloadTooLargeStream();
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
            }),
            new EphemeralDataProtectionProvider(),
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenChunkedPayloadExceedsLimit_IsRefusedWithoutBufferingTheWholeBody()
    {
        // Arrange
        var inbox = CreateInbox();
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        var body = new EndlessStream();

        // A caller that wants to send more than it is allowed to simply omits the content length.
        httpContext.Request.Body = body;

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
            }),
            new EphemeralDataProtectionProvider(),
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.True(
            body.BytesProduced < DialpadWebhookEndpoint.MaximumRequestBodySizeBytes * 2,
            $"The endpoint read {body.BytesProduced} bytes of a body it is not willing to accept.");

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
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));
        httpContext.RequestAborted = new CancellationTokenSource().Token;
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
            .CreateProtector(DialpadConstants.WebhookProtectorName)
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
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
        using var consumedLease = await limiter.AcquireRateAsync(DialpadConstants.ProviderTechnicalName, TestContext.Current.CancellationToken);
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, limiter);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var timestamp = new DateTimeOffset(_now.AddSeconds(offsetSeconds)).ToUnixTimeMilliseconds();
        var payload = $"{{\"call_id\":\"c1\",\"state\":\"ringing\",\"event_timestamp\":{timestamp}}}";
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(payload, secret)));

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
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
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var inbox = CreateInbox();
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext(inbox.Object, _ingressLimiter);
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(payload, secret)));

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        inbox.Verify(
            service => service.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Call_WhenDurableInboxIsUnavailable_ProcessesWebhookInline()
    {
        // Arrange
        const string secret = "shhh";
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var webhookService = CreateWebhookService();
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(_payload, secret)));
        httpContext.RequestAborted = new CancellationTokenSource().Token;

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        webhookService.Verify(
            service => service.ProcessAsync(
                It.IsAny<DialpadCallEvent>(),
                It.Is<CancellationToken>(token => !token.CanBeCanceled)),
            Times.Once);
    }

    [Fact]
    public async Task Call_WhenSignedPayloadUsesCurrentDialpadShape_ProcessesWebhookInline()
    {
        // Arrange
        const string secret = "shhh";
        var eventTimestamp = new DateTimeOffset(_now).ToUnixTimeMilliseconds();
        var payload = """
            {"date_started":1787158716092,"call_id":5635304278851584,"state":"ringing","direction":"outbound","external_number":"+17024993350","internal_number":"blocked","selected_caller_id":null,"date_rang":null,"date_first_rang":1787158716151,"date_queued":null,"target_availability_status":"open","callback_requested":null,"date_connected":null,"date_ended":1787158716555,"talk_time":0,"hold_time":0,"duration":0,"total_duration":463.294,"contact":{"id":6051533200162816,"type":"local","email":"","phone":"+17024993350","name":"(702) 499-3350"},"target":{"id":5171365938069504,"type":"user","email":"mike@crestapps.com","phone":"+12088208280","name":"Mike Alhayek","office_id":5716039329251328},"entry_point_call_id":null,"entry_point_target":{},"operator_call_id":null,"proxy_target":{},"group_id":null,"master_call_id":null,"is_internal":false,"is_transferred":false,"transferred_from":{},"csat_score":null,"routing_breadcrumbs":[],"event_timestamp":{{eventTimestamp}},"mos_score":null,"labels":[],"was_recorded":false,"voicemail_link":null,"voicemail_recording_id":null,"call_recording_ids":[],"transcription_text":null,"recording_details":[],"integrations":{}}
            """.Replace("{{eventTimestamp}}", eventTimestamp.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        DialpadCallEvent capturedCallEvent = null;
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedSecret = dataProtectionProvider
            .CreateProtector(DialpadConstants.WebhookProtectorName)
            .Protect(secret);
        var webhookService = new Mock<IDialpadWebhookService>();
        webhookService
            .Setup(service => service.ProcessAsync(It.IsAny<DialpadCallEvent>(), It.IsAny<CancellationToken>()))
            .Callback<DialpadCallEvent, CancellationToken>((callEvent, _) => capturedCallEvent = callEvent)
            .ReturnsAsync(DialpadWebhookResult.Updated);
        var httpContext = CreateHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(CreateJwt(payload, secret)));

        // Act
        var result = await DialpadWebhookEndpoint.HandleAsync(
            SiteServiceFactory.Create(new DialpadSettings
            {
                IsEnabled = true,
                Production = new DialpadEnvironmentSettings
                {
                    WebhookSigningSecret = protectedSecret,
                },
            }),
            dataProtectionProvider,
            webhookService.Object,
            CreateClock(),
            NullLogger<DialpadStartup>.Instance,
            httpContext);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, Assert.IsAssignableFrom<IStatusCodeHttpResult>(result).StatusCode);
        Assert.NotNull(capturedCallEvent);
        Assert.Equal("5635304278851584", capturedCallEvent.CallId);
        Assert.Equal("+17024993350", capturedCallEvent.ExternalNumber);
        Assert.Equal("+12088208280", capturedCallEvent.Target);
        Assert.Equal("(702) 499-3350", capturedCallEvent.ContactName);
    }

    public void Dispose()
    {
        _ingressLimiter.Dispose();
    }

    private static DefaultHttpContext CreateHttpContext(
        IProviderWebhookInbox inbox = null,
        IProviderWebhookIngressLimiter ingressLimiter = null)
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream("signed-payload"u8.ToArray());
        var services = new ServiceCollection();

        if (inbox is not null)
        {
            services.AddSingleton(inbox);
        }

        if (ingressLimiter is not null)
        {
            services.AddSingleton(ingressLimiter);
        }

        httpContext.RequestServices = services.BuildServiceProvider();

        return httpContext;
    }

    private static IClock CreateClock()
    {
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);

        return clock.Object;
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

    private static Mock<IDialpadWebhookService> CreateWebhookService()
    {
        var webhookService = new Mock<IDialpadWebhookService>();
        webhookService
            .Setup(service => service.ProcessAsync(It.IsAny<DialpadCallEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(DialpadWebhookResult.Updated);

        return webhookService;
    }
}
