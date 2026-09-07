using System.Text;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;
using OrchardCore.Settings;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Signers;
using Org.BouncyCastle.Security;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// What the webhook endpoint tells Telnyx once a delivery is durably stored.
/// </summary>
/// <remarks>
/// The endpoint stores the delivery, then tries to process it inline as a latency optimization, and a background
/// worker finishes anything the inline attempt does not. So once the store succeeds, the answer to the provider is
/// settled: it landed. Answering with a failure instead asks the provider to send it again — and a provider that
/// redelivers while the store is already contended makes the contention worse. One live call's teardown burst did
/// exactly that: eight webhooks came back 503, one came back 500 from an unhandled SQLite "database is locked",
/// and Telnyx kept re-sending deliveries the platform had already stored.
/// </remarks>
public sealed class TelnyxWebhookInboxDurabilityTests
{
    [Fact]
    public async Task ADeliveryThatIsStored_IsAcknowledged_EvenWhenProcessingItFails()
    {
        // Arrange
        // The store succeeded, so the delivery is safe and the background worker owns finishing it.
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(x => x.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Accepted,
                MessageId = "message-1",
            });
        inbox
            .Setup(x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQLite Error 5: 'database is locked'."));

        // Act
        var status = await InvokeAsync(inbox.Object);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task ADeliveryThatCouldNotBeStored_IsRefusedSoTheProviderSendsItAgain()
    {
        // Arrange
        // Nothing was written, so nothing will pick this up later. This is the one case where asking the provider
        // to redeliver is right.
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(x => x.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQLite Error 5: 'database is locked'."));

        // Act
        var status = await InvokeAsync(inbox.Object);

        // Assert
        // Retryable, and a status rather than an unhandled exception: a 500 error page on a store that is already
        // struggling is load nobody asked for.
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task AFailureToStore_IsNotReportedAsAProcessedWebhook()
    {
        // Arrange
        // The health metric drives the companion tooling's view of delivery loss, so a delivery that was dropped
        // has to count as one.
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(x => x.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SQLite Error 5: 'database is locked'."));

        var metrics = new Mock<ISoftPhoneHealthMetrics>();

        // Act
        await InvokeAsync(inbox.Object, metrics.Object);

        // Assert
        metrics.Verify(x => x.RecordWebhookProcessed(false), Times.Once);
        metrics.Verify(x => x.RecordWebhookProcessed(true), Times.Never);
    }

    [Fact]
    public async Task ADeliveryTheInboxIsTooBusyToTake_IsRefusedRatherThanDropped()
    {
        // Arrange
        // Busy already means "not stored". It has always answered retryable; this pins it so the new error
        // handling around it does not quietly turn a refusal into an acknowledgement.
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(x => x.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Busy,
            });

        // Act
        var status = await InvokeAsync(inbox.Object);

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
        inbox.Verify(x => x.DispatchAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ADeliveryAlreadySeen_IsAcknowledgedWithoutBeingStoredTwice()
    {
        // Arrange
        // Telnyx redelivers on any non-2xx, so duplicates are normal traffic rather than an error, and the
        // endpoint has to settle them instead of provoking another round.
        var inbox = new Mock<IProviderWebhookInbox>();
        inbox
            .Setup(x => x.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
            {
                Status = ProviderWebhookInboxAcceptanceStatus.Duplicate,
                MessageId = "message-1",
            });

        // Act
        var status = await InvokeAsync(inbox.Object);

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    /// <summary>
    /// Drives the real endpoint with a genuinely signed delivery, and reports the status code it answered.
    /// </summary>
    private static async Task<int> InvokeAsync(
        IProviderWebhookInbox inbox,
        ISoftPhoneHealthMetrics healthMetrics = null)
    {
        const string Body = """{"data":{"event_type":"call.hangup","payload":{"call_control_id":"v3:call-1"}}}""";

        var key = GenerateKey();
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var protectedKey = dataProtectionProvider
            .CreateProtector(TelnyxConstants.WebhookProtectorName)
            .Protect(key.PublicKeyBase64);

        var clock = new Mock<IClock>();
        var now = new DateTime(2026, 9, 6, 21, 34, 8, DateTimeKind.Utc);
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var timestamp = new DateTimeOffset(now).ToUnixTimeSeconds().ToString(System.Globalization.CultureInfo.InvariantCulture);

        var siteService = new Mock<ISiteService>();
        siteService
            .Setup(x => x.GetSiteSettingsAsync())
            .ReturnsAsync(BuildSite(new TelnyxSettings
            {
                IsEnabled = true,
                WebhookPublicKey = protectedKey,
            }));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(inbox);

        if (healthMetrics is not null)
        {
            services.AddSingleton(healthMetrics);
        }

        await using var serviceProvider = services.BuildServiceProvider();

        var httpContext = new DefaultHttpContext
        {
            RequestServices = serviceProvider,
        };

        var bodyBytes = Encoding.UTF8.GetBytes(Body);
        httpContext.Request.Body = new MemoryStream(bodyBytes);
        httpContext.Request.ContentLength = bodyBytes.Length;
        httpContext.Request.ContentType = "application/json";
        httpContext.Request.Headers[TelnyxConstants.SignatureHeaderName] = Sign(key, timestamp, Body);
        httpContext.Request.Headers[TelnyxConstants.TimestampHeaderName] = timestamp;

        var result = await TelnyxWebhookEndpoint.HandleAsync(
            siteService.Object,
            dataProtectionProvider,
            new Mock<ITelnyxWebhookService>().Object,
            clock.Object,
            NullLogger<CrestApps.OrchardCore.Telnyx.Startup>.Instance,
            httpContext);

        await result.ExecuteAsync(httpContext);

        return httpContext.Response.StatusCode;
    }

    private static ISite BuildSite(TelnyxSettings settings)
    {
        // Set both the typed accessor and the raw property bag, so the test does not depend on which one the
        // settings extension happens to read through.
        var properties = new System.Text.Json.Nodes.JsonObject
        {
            [nameof(TelnyxSettings)] = System.Text.Json.JsonSerializer.SerializeToNode(settings),
        };

        var site = new Mock<ISite>();
        site.SetupGet(x => x.Properties).Returns(properties);
        site.Setup(x => x.GetOrCreate<TelnyxSettings>()).Returns(settings);
        site.Setup(x => x.As<TelnyxSettings>()).Returns(settings);

        return site.Object;
    }

    private static (string PublicKeyBase64, Ed25519PrivateKeyParameters PrivateKey) GenerateKey()
    {
        var random = new SecureRandom();
        var privateKey = new Ed25519PrivateKeyParameters(random);

        return (Convert.ToBase64String(privateKey.GeneratePublicKey().GetEncoded()), privateKey);
    }

    private static string Sign((string PublicKeyBase64, Ed25519PrivateKeyParameters PrivateKey) key, string timestamp, string body)
    {
        var payload = Encoding.UTF8.GetBytes($"{timestamp}|{body}");
        var signer = new Ed25519Signer();
        signer.Init(true, key.PrivateKey);
        signer.BlockUpdate(payload, 0, payload.Length);

        return Convert.ToBase64String(signer.GenerateSignature());
    }
}
