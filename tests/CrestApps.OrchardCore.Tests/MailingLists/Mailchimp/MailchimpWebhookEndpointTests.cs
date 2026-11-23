using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CrestApps.Core;
using CrestApps.OrchardCore.Core.Security;
using CrestApps.OrchardCore.MailingLists.Mailchimp;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Endpoints;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Models;
using CrestApps.OrchardCore.MailingLists.Mailchimp.Services;
using CrestApps.OrchardCore.MailingLists.Services;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Environment.Shell;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.MailingLists.Mailchimp;

public sealed class MailchimpWebhookEndpointTests
{
    private const string UrlSecret = "0123456789abcdef0123456789abcdef";
    private const string SigningSecret = "signing-secret-1";
    private const string Body = "type=unsubscribe&fired_at=2026-11-22+10%3A00%3A00&data%5Baction%5D=unsub&data%5Blist_id%5D=aud123&data%5Bemail%5D=a%40example.com";

    private static readonly DateTime _nowUtc = new(2026, 11, 22, 10, 0, 10, DateTimeKind.Utc);

    [Fact]
    public async Task ADeliveryWithTheRightSecret_IsStoredInTheInbox_KeyedByTheListAndTheBody()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        var delivery = Assert.Single(harness.Deliveries);
        Assert.Equal(MailchimpConstants.WebhookProviderName, delivery.ProviderName);
        Assert.Equal(MailchimpConstants.WebhookHandlerName, delivery.HandlerName);
        Assert.Equal(Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes("list-1\n" + Body))), delivery.DeliveryId);

        var payload = JsonSerializer.Deserialize<MailchimpWebhookPayload>(delivery.Payload, JsonSerializerOptions.Web);
        Assert.Equal("list-1", payload.ListId);
        Assert.Equal(Body, payload.Body);
        Assert.Equal(_nowUtc, payload.ReceivedUtc);
    }

    [Fact]
    public void DeliveryId_IsTheSameForARedelivery_AndDiffersForAnotherListOnTheSameAudience()
    {
        // Act
        var first = MailchimpWebhookEndpoint.GetDeliveryId("list-1", Body);
        var redelivery = MailchimpWebhookEndpoint.GetDeliveryId("list-1", Body);
        var otherList = MailchimpWebhookEndpoint.GetDeliveryId("list-2", Body);

        // Assert
        Assert.Equal(first, redelivery);
        Assert.NotEqual(first, otherList);
        Assert.Equal(64, first.Length);
    }

    [Fact]
    public async Task ADuplicateDelivery_IsAcknowledged()
    {
        // Arrange
        var harness = new Harness(acceptance: ProviderWebhookInboxAcceptanceStatus.Duplicate);

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
    }

    [Fact]
    public async Task ABusyInbox_AsksMailchimpToSendItAgain()
    {
        // Arrange
        var harness = new Harness(acceptance: ProviderWebhookInboxAcceptanceStatus.Busy);

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Fact]
    public async Task AnInboxThatCannotStore_AsksMailchimpToSendItAgain()
    {
        // Arrange
        var harness = new Harness(acceptFails: true);

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status);
    }

    [Theory]
    [InlineData("wrong-secret")]
    [InlineData("")]
    public async Task AWrongUrlSecret_IsNotFound_AndNothingIsStored(string secret)
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync(secret: secret);

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task AnUnknownList_IsNotFound()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync(listId: "list-9");

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task AListWithoutAWebhook_IsNotFound()
    {
        // Arrange
        var harness = new Harness(registered: false);

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status404NotFound, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task ASignedWebhook_WithAValidRecentSignature_IsStored()
    {
        // Arrange
        var harness = new Harness(signed: true);
        var timestamp = UnixSeconds(_nowUtc.AddSeconds(-30));

        // Act
        var status = await harness.InvokeAsync(signature: $"t={timestamp},v1={Sign(SigningSecret, timestamp, Body)}");

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Single(harness.Deliveries);
    }

    [Fact]
    public async Task ASignedWebhook_WithoutASignature_IsRefused()
    {
        // Arrange
        var harness = new Harness(signed: true);

        // Act
        var status = await harness.InvokeAsync();

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task ASignedWebhook_SignedWithAnotherSecret_IsRefused()
    {
        // Arrange
        var harness = new Harness(signed: true);
        var timestamp = UnixSeconds(_nowUtc);

        // Act
        var status = await harness.InvokeAsync(signature: $"t={timestamp},v1={Sign("another-secret", timestamp, Body)}");

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task ASignedWebhook_ReplayedAfterFiveMinutes_IsRefused()
    {
        // Arrange
        var harness = new Harness(signed: true);
        var timestamp = UnixSeconds(_nowUtc.AddMinutes(-6));

        // Act
        var status = await harness.InvokeAsync(signature: $"t={timestamp},v1={Sign(SigningSecret, timestamp, Body)}");

        // Assert
        Assert.Equal(StatusCodes.Status401Unauthorized, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task AWebhookAboutAnotherAudience_IsRefused()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync(body: Body.Replace("aud123", "other9", StringComparison.Ordinal));

        // Assert
        Assert.Equal(StatusCodes.Status400BadRequest, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task ACampaignEvent_IsAcknowledged_ButNotStored()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync(body: "type=campaign&fired_at=2026-11-22+10%3A00%3A00&data%5Bid%5D=5aa2102003&data%5Blist_id%5D=aud123");

        // Assert
        Assert.Equal(StatusCodes.Status200OK, status);
        Assert.Empty(harness.Deliveries);
    }

    [Fact]
    public async Task ABodyLargerThanTheCap_IsRefused()
    {
        // Arrange
        var harness = new Harness();

        // Act
        var status = await harness.InvokeAsync(body: "type=profile&data%5Bmerges%5D%5BNOTE%5D=" + new string('x', MailchimpWebhookEndpoint.MaxBodyLength));

        // Assert
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, status);
        Assert.Empty(harness.Deliveries);
    }

    private static string UnixSeconds(DateTime utc)
        => new DateTimeOffset(utc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

    private static string Sign(string secret, string timestamp, string body)
        => Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    private sealed class Harness
    {
        private readonly EphemeralDataProtectionProvider _dataProtectionProvider = new();
        private readonly Mock<IMailingListManager> _lists = new();
        private readonly Mock<IProviderWebhookInbox> _inbox = new();

        public Harness(
            bool registered = true,
            bool signed = false,
            ProviderWebhookInboxAcceptanceStatus acceptance = ProviderWebhookInboxAcceptanceStatus.Accepted,
            bool acceptFails = false)
        {
            var list = MailchimpTestContext.List();
            var settings = list.GetMailchimpSettings();

            if (registered)
            {
                settings.WebhookId = "webhook-1";
                settings.WebhookUrlSecret = _dataProtectionProvider.ProtectSetting(MailchimpConstants.WebhookSecretPurpose, UrlSecret);
                settings.WebhookSigningSecret = signed ? _dataProtectionProvider.ProtectSetting(MailchimpConstants.WebhookSecretPurpose, SigningSecret) : null;
            }

            list.Put(settings);

            _lists
                .Setup(manager => manager.FindByIdAsync("list-1", It.IsAny<CancellationToken>()))
                .ReturnsAsync(list);

            var accept = _inbox.Setup(inbox => inbox.AcceptAsync(It.IsAny<ProviderWebhookInboxDelivery>(), It.IsAny<CancellationToken>()));

            if (acceptFails)
            {
                accept.ThrowsAsync(new InvalidOperationException("SQLite Error 5: 'database is locked'."));
            }
            else
            {
                accept
                    .Callback<ProviderWebhookInboxDelivery, CancellationToken>((delivery, _) => Deliveries.Add(delivery))
                    .ReturnsAsync(new ProviderWebhookInboxAcceptanceResult
                    {
                        Status = acceptance,
                        MessageId = "message-1",
                    });
            }
        }

        public List<ProviderWebhookInboxDelivery> Deliveries { get; } = [];

        public async Task<int> InvokeAsync(string listId = "list-1", string secret = UrlSecret, string body = Body, string signature = null)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton(_inbox.Object);

            await using var serviceProvider = services.BuildServiceProvider();

            var httpContext = new DefaultHttpContext
            {
                RequestServices = serviceProvider,
            };

            var bytes = Encoding.UTF8.GetBytes(body);
            httpContext.Request.Method = HttpMethods.Post;
            httpContext.Request.Body = new MemoryStream(bytes);
            httpContext.Request.ContentLength = bytes.Length;
            httpContext.Request.ContentType = "application/x-www-form-urlencoded";

            if (signature is not null)
            {
                httpContext.Request.Headers[MailchimpConstants.SignatureHeaderName] = signature;
            }

            var clock = new Mock<IClock>();
            clock.SetupGet(value => value.UtcNow).Returns(_nowUtc);

            var result = await MailchimpWebhookEndpoint.HandleAsync(
                listId,
                secret,
                httpContext,
                _lists.Object,
                _dataProtectionProvider,
                clock.Object,
                new Mock<IShellHost>().Object,
                new ShellSettings(),
                NullLogger<MailchimpWebhookInboxHandler>.Instance);

            await result.ExecuteAsync(httpContext);

            return httpContext.Response.StatusCode;
        }
    }
}
