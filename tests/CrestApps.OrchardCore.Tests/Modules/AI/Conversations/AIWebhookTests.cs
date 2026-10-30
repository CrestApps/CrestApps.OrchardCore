using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.AI.Core.Webhooks;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.AI.Conversations;

public sealed class AIWebhookTests
{
    private static readonly DateTime _now = new(2026, 10, 28, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task PublishAsync_QueuesOnlyForEnabledSubscriptionsOfTheEvent()
    {
        // Arrange
        var created = new List<AIWebhookDelivery>();
        var subscriptions = new Mock<IAIWebhookSubscriptionStore>();
        subscriptions.Setup(x => x.ListEnabledAsync(It.IsAny<CancellationToken>())).ReturnsAsync(
        [
            new AIWebhookSubscription { ItemId = "s1", Events = [AIWebhookEvents.ConversationEnded] },
            new AIWebhookSubscription { ItemId = "s2", Events = [AIWebhookEvents.AlertTriggered] },
        ]);
        var deliveries = new Mock<IAIWebhookDeliveryStore>();
        deliveries.Setup(x => x.CreateAsync(It.IsAny<AIWebhookDelivery>(), It.IsAny<CancellationToken>()))
            .Callback<AIWebhookDelivery, CancellationToken>((delivery, _) => created.Add(delivery))
            .Returns(ValueTask.CompletedTask);
        var publisher = new AIWebhookPublisher(subscriptions.Object, deliveries.Object, Clock());

        // Act
        await publisher.PublishAsync(AIWebhookEvents.ConversationEnded, new { sessionId = "abc" }, TestContext.Current.CancellationToken);

        // Assert
        var delivery = Assert.Single(created);
        Assert.Equal("s1", delivery.SubscriptionId);
        Assert.Equal(AIWebhookDeliveryStatus.Pending, delivery.Status);

        using var body = JsonDocument.Parse(delivery.Payload);
        Assert.Equal(AIWebhookEvents.ConversationEnded, body.RootElement.GetProperty("event").GetString());
        Assert.Equal("abc", body.RootElement.GetProperty("data").GetProperty("sessionId").GetString());
        Assert.Equal(delivery.ItemId, body.RootElement.GetProperty("id").GetString());
    }

    [Fact]
    public async Task SendAsync_SignsTheBodyAndMarksItDelivered()
    {
        // Arrange
        var handler = new CapturingHandler(HttpStatusCode.OK);
        var protector = new EphemeralDataProtectionProvider();
        var subscription = new AIWebhookSubscription
        {
            ItemId = "s1",
            Url = "https://example.com/hook",
            ProtectedSecret = protector.CreateProtector(AIWebhookPublisher.ProtectorPurpose).Protect("shh"),
        };
        var delivery = new AIWebhookDelivery { ItemId = "d1", EventName = "conversation.ended", Payload = "{\"a\":1}" };
        var sender = new AIWebhookSender(Factory(handler), protector, Clock(), NullLogger<AIWebhookSender>.Instance);

        // Act
        await sender.SendAsync(delivery, subscription, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIWebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(200, delivery.StatusCode);

        var request = Assert.Single(handler.Requests);
        var timestamp = long.Parse(request.Headers.GetValues("X-Webhook-Timestamp").Single(), System.Globalization.CultureInfo.InvariantCulture);
        Assert.Equal(AIWebhookSender.Sign("shh", timestamp, "{\"a\":1}"), request.Headers.GetValues("X-Webhook-Signature").Single());
        Assert.Equal("conversation.ended", request.Headers.GetValues("X-Webhook-Event").Single());
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, AIWebhookDeliveryStatus.Pending)]
    [InlineData(HttpStatusCode.TooManyRequests, AIWebhookDeliveryStatus.Pending)]
    [InlineData(HttpStatusCode.BadRequest, AIWebhookDeliveryStatus.Failed)]
    public async Task SendAsync_RetriesOnlyWhatIsWorthRetrying(HttpStatusCode answer, AIWebhookDeliveryStatus expected)
    {
        // Arrange
        var subscription = new AIWebhookSubscription { ItemId = "s1", Url = "https://example.com/hook" };
        var delivery = new AIWebhookDelivery { ItemId = "d1", EventName = "x", Payload = "{}" };
        var sender = new AIWebhookSender(Factory(new CapturingHandler(answer)), new EphemeralDataProtectionProvider(), Clock(), NullLogger<AIWebhookSender>.Instance);

        // Act
        await sender.SendAsync(delivery, subscription, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(expected, delivery.Status);

        if (expected == AIWebhookDeliveryStatus.Pending)
        {
            Assert.Equal(_now + AIWebhookSender.RetryDelays[0], delivery.NextAttemptUtc);
        }
    }

    [Fact]
    public async Task SendAsync_GivesUpAfterTheLastRetry()
    {
        // Arrange
        var subscription = new AIWebhookSubscription { ItemId = "s1", Url = "https://example.com/hook" };
        var delivery = new AIWebhookDelivery { ItemId = "d1", EventName = "x", Payload = "{}", Attempts = AIWebhookSender.RetryDelays.Count };
        var sender = new AIWebhookSender(Factory(new CapturingHandler(HttpStatusCode.ServiceUnavailable)), new EphemeralDataProtectionProvider(), Clock(), NullLogger<AIWebhookSender>.Instance);

        // Act
        await sender.SendAsync(delivery, subscription, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIWebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Equal(_now, delivery.CompletedUtc);
    }

    [Fact]
    public async Task SendAsync_FailsADeliveryWhoseSubscriptionIsGone()
    {
        // Arrange
        var delivery = new AIWebhookDelivery { ItemId = "d1", EventName = "x", Payload = "{}" };
        var sender = new AIWebhookSender(Factory(new CapturingHandler(HttpStatusCode.OK)), new EphemeralDataProtectionProvider(), Clock(), NullLogger<AIWebhookSender>.Instance);

        // Act
        await sender.SendAsync(delivery, null, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(AIWebhookDeliveryStatus.Failed, delivery.Status);
    }

    private static IClock Clock()
        => Mock.Of<IClock>(clock => clock.UtcNow == _now);

    private static IHttpClientFactory Factory(HttpMessageHandler handler)
        => Mock.Of<IHttpClientFactory>(factory => factory.CreateClient(It.IsAny<string>()) == new HttpClient(handler, disposeHandler: false));

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

            return Task.FromResult(new HttpResponseMessage(_statusCode));
        }
    }
}
