using System.Net;
using System.Net.Http;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using CrestApps.OrchardCore.Core.Http;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// The one outbound webhooks building block: modules declare events, the publisher queues them in the outbox, the
/// dispatcher signs and sends them through the egress guard, and retries with backoff.
/// </summary>
public sealed class OutboundWebhookTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task Publish_QueuesOnlyForEnabledSubscriptionsThatTakeTheEvent()
    {
        // Arrange
        var created = new List<WebhookDelivery>();
        var publisher = CreatePublisher(
            [
                new WebhookSubscription { ItemId = "s1", Events = ["order.paid"] },
                new WebhookSubscription { ItemId = "s2", Events = ["order.canceled"] },
                new WebhookSubscription { ItemId = "s3", Events = [WebhookEvents.All] },
            ],
            created,
            new WebhookEventDescriptor("order.paid", "Order paid", "Orders"));

        // Act
        await publisher.PublishAsync("order.paid", new { orderId = "o1" }, "evt_fixed", TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(["s1", "s3"], created.Select(delivery => delivery.SubscriptionId).Order());
        Assert.All(created, delivery =>
        {
            Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
            Assert.Equal("evt_fixed", delivery.EventId);

            using var body = JsonDocument.Parse(delivery.Payload);
            Assert.Equal("order.paid", body.RootElement.GetProperty("event").GetString());
            Assert.Equal("o1", body.RootElement.GetProperty("data").GetProperty("orderId").GetString());
        });
    }

    [Fact]
    public async Task Publish_SendsNothing_ForAnEventNoEnabledModuleDeclares()
    {
        // Arrange
        var created = new List<WebhookDelivery>();
        var publisher = CreatePublisher([new WebhookSubscription { ItemId = "s1", Events = [WebhookEvents.All] }], created);

        // Act
        await publisher.PublishAsync("conversation.ended", new { }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(created);
    }

    [Fact]
    public async Task Sender_SignsTheBody_WithTheSharedHeaders()
    {
        // Arrange
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var sender = CreateSender(handler);

        // Act
        var result = await sender.SendAsync(new WebhookSendRequest
        {
            Url = "https://hooks.example.com/in",
            Secret = "shh",
            EventId = "evt_1",
            EventName = "order.paid",
            Body = "{\"a\":1}",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        var request = Assert.Single(handler.Requests);
        var timestamp = long.Parse(request.Headers.GetValues(WebhookHeaders.Timestamp).Single(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(new DateTimeOffset(_now).ToUnixTimeSeconds(), timestamp);
        Assert.Equal("evt_1", request.Headers.GetValues(WebhookHeaders.Id).Single());
        Assert.Equal("order.paid", request.Headers.GetValues(WebhookHeaders.Event).Single());
        Assert.True(new HmacSha256WebhookSigner().Verify("shh", timestamp, "{\"a\":1}", request.Headers.GetValues(WebhookHeaders.Signature).Single()));
    }

    [Fact]
    public async Task ARefusedHost_FailsAtOnce_InsteadOfBeingRetried()
    {
        // Arrange
        var delivery = PendingDelivery();
        var dispatcher = CreateDispatcher(new ThrowingHandler(new HttpRequestException($"The host 10.0.0.5 {EgressNetwork.RefusedMessage}.")), delivery);

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.NextAttemptUtc);
        Assert.Contains(EgressNetwork.RefusedMessage, delivery.LastError, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AFailedAttempt_IsRetriedAfterItsBackoff_ThenGivenUp()
    {
        // Arrange
        var delivery = PendingDelivery();
        var dispatcher = CreateDispatcher(new CapturingHandler(HttpStatusCode.ServiceUnavailable), delivery);

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(_now + WebhookDispatcher.Backoff[0], delivery.NextAttemptUtc);
        Assert.Equal(503, delivery.LastStatusCode);

        // The last allowed attempt gives up.
        delivery.Attempts = WebhookDispatcher.Backoff.Count;
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.NextAttemptUtc);
    }

    [Fact]
    public async Task ASuccessfulAttempt_DeliversIt()
    {
        // Arrange
        var delivery = PendingDelivery();
        var dispatcher = CreateDispatcher(new CapturingHandler(HttpStatusCode.Accepted), delivery);

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(_now, delivery.DeliveredUtc);
        Assert.Null(delivery.LastError);
    }

    [Fact]
    public void Catalog_GathersEveryProvidersEvents_OncePerName()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddLogging()
            .AddLocalization()
            .AddWebhooks()
            .AddWebhookEvent("order.paid", "Order paid", "Orders")
            .AddWebhookEvent("order.paid", "Order paid twice", "Orders")
            .AddScoped<IWebhookEventProvider, AIWebhookEventProvider>();

        services.AddSingleton(typeof(IStringLocalizer<>), typeof(PassThroughLocalizer<>));
        services.AddSingleton(Mock.Of<global::YesSql.ISession>());

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        // Act
        var catalog = scope.ServiceProvider.GetRequiredService<IWebhookEventCatalog>();
        var events = catalog.GetEvents();

        // Assert
        Assert.Equal("Order paid", Assert.Single(events, entry => entry.Name == "order.paid").DisplayName);
        Assert.Contains(events, entry => entry.Name == AIWebhookEvents.ConversationEnded);
        Assert.Equal(AIWebhookEvents.All.Count + 1, events.Count);
        Assert.NotNull(catalog.Find(AIWebhookEvents.AlertTriggered));
        Assert.Null(catalog.Find("unknown.event"));
    }

    [Fact]
    public void StoredRecords_KeepTheTypeNamesCommerceWebhooksStoredThemUnder()
    {
        Assert.Equal("CrestApps.OrchardCore.Commerce.Webhooks.Models.WebhookSubscription, CrestApps.OrchardCore.Commerce.Webhooks", WebhookSubscription.StoredTypeName);
        Assert.Equal("CrestApps.OrchardCore.Commerce.Webhooks.Models.WebhookDelivery, CrestApps.OrchardCore.Commerce.Webhooks", WebhookDelivery.StoredTypeName);
        Assert.Equal("Webhooks", WebhookStorage.CollectionName);
        Assert.Equal("CrestApps.OrchardCore.Commerce.Webhooks.Secret", WebhookStorage.SecretProtectionPurpose);
    }

    private static WebhookPublisher CreatePublisher(IReadOnlyList<WebhookSubscription> subscriptions, List<WebhookDelivery> created, params WebhookEventDescriptor[] declared)
    {
        var subscriptionStore = new Mock<IWebhookSubscriptionStore>();
        subscriptionStore.Setup(store => store.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(subscriptions);
        var deliveryStore = new Mock<IWebhookDeliveryStore>();
        deliveryStore.Setup(store => store.CreateAsync(It.IsAny<WebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookDelivery, CancellationToken>((delivery, _) => created.Add(delivery))
            .Returns(ValueTask.CompletedTask);
        var provider = new Mock<IWebhookEventProvider>();
        provider.Setup(entry => entry.GetEvents()).Returns(declared);

        return new WebhookPublisher(subscriptionStore.Object, deliveryStore.Object, new WebhookEventCatalog([provider.Object]), Clock(), NullLogger<WebhookPublisher>.Instance);
    }

    private static WebhookDispatcher CreateDispatcher(HttpMessageHandler handler, WebhookDelivery delivery)
    {
        var protection = new EphemeralDataProtectionProvider();
        var subscription = new WebhookSubscription
        {
            ItemId = delivery.SubscriptionId,
            Url = "https://hooks.example.com/in",
            Events = [WebhookEvents.All],
            ProtectedSecret = protection.CreateProtector(WebhookStorage.SecretProtectionPurpose).Protect("shh"),
        };
        var subscriptions = new Mock<IWebhookSubscriptionStore>();
        subscriptions.Setup(store => store.FindByIdAsync(subscription.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        return new WebhookDispatcher(
            CreateSender(handler),
            Mock.Of<IWebhookDeliveryStore>(),
            subscriptions.Object,
            protection,
            Clock(),
            NullLogger<WebhookDispatcher>.Instance);
    }

    private static WebhookSender CreateSender(HttpMessageHandler handler)
        => new(
            Mock.Of<IHttpClientFactory>(factory => factory.CreateClient(It.IsAny<string>()) == new HttpClient(handler, false)),
            new HmacSha256WebhookSigner(),
            Clock(),
            NullLogger<WebhookSender>.Instance);

    private static WebhookDelivery PendingDelivery()
        => new()
        {
            ItemId = "d1",
            SubscriptionId = "s1",
            EventId = "evt_1",
            EventName = "order.paid",
            Payload = "{}",
            Status = WebhookDeliveryStatus.Pending,
        };

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public CapturingHandler(HttpStatusCode statusCode)
        {
            _statusCode = statusCode;
        }

        public List<HttpRequestMessage> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request);

            return Task.FromResult(new HttpResponseMessage(_statusCode) { Content = new StringContent("answer") });
        }
    }

    private sealed class ThrowingHandler : HttpMessageHandler
    {
        private readonly Exception _exception;

        public ThrowingHandler(Exception exception)
        {
            _exception = exception;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => throw _exception;
    }

    private sealed class PassThroughLocalizer<T> : IStringLocalizer<T>
    {
        public LocalizedString this[string name] => new(name, name);

        public LocalizedString this[string name, params object[] arguments] => new(name, string.Format(System.Globalization.CultureInfo.InvariantCulture, name, arguments));

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }
}
