using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.Webhooks;

/// <summary>
/// A direct delivery goes to its own address, signed by the formatter it names instead of a subscription's secret;
/// when nothing can format it, or the formatter refuses, it fails at once rather than being tried again.
/// </summary>
public sealed class WebhookDispatcherDirectDeliveryTests
{
    private static readonly DateTime _now = new(2026, 11, 4, 12, 0, 0, DateTimeKind.Utc);

    private readonly List<WebhookSendRequest> _sent = [];
    private readonly Mock<IWebhookDeliveryStore> _deliveries = new();
    private readonly Mock<IWebhookSubscriptionStore> _subscriptions = new();

    [Fact]
    public async Task SendAsync_SendsADirectDeliveryThroughItsFormatterUnsigned()
    {
        // Arrange
        var formatter = new FakeFormatter("acp", new WebhookFormattedRequest
        {
            Headers = new Dictionary<string, string> { ["Merchant-Signature"] = "t=1,v1=abc" },
        });
        var dispatcher = CreateDispatcher(new WebhookSendResult { Succeeded = true, StatusCode = 200 }, formatter);
        var delivery = DirectDelivery("acp");

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        var request = Assert.Single(_sent);

        Assert.Equal("https://agent.example.com/webhooks", request.Url);
        Assert.False(request.Sign);
        Assert.Null(request.Secret);
        Assert.Equal("{\"type\":\"order_create\"}", request.Body);
        Assert.Equal("order_create", request.EventName);
        Assert.Equal("t=1,v1=abc", request.Headers["Merchant-Signature"]);

        var context = Assert.Single(formatter.Contexts);

        Assert.Equal("https://agent.example.com/webhooks", context.Url);
        Assert.Equal("{\"type\":\"order_create\"}", context.Body);
        Assert.Equal(new DateTimeOffset(_now).ToUnixTimeSeconds(), context.Timestamp);
        Assert.Equal("platform-1", context.Data["platformId"]);

        Assert.Equal(WebhookDeliveryStatus.Delivered, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal(_now, delivery.DeliveredUtc);
        _subscriptions.Verify(store => store.FindByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _deliveries.Verify(store => store.UpdateAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithoutItsFormatter_FailsWithoutRetry()
    {
        // Arrange
        var dispatcher = CreateDispatcher(new WebhookSendResult { Succeeded = true }, new FakeFormatter("other", new WebhookFormattedRequest()));
        var delivery = DirectDelivery("acp");

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_sent);
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.NextAttemptUtc);
        Assert.Equal(1, delivery.Attempts);
        Assert.Contains("'acp'", delivery.LastError, StringComparison.Ordinal);
        _deliveries.Verify(store => store.UpdateAsync(delivery, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WhenTheFormatterRefuses_FailsWithItsError()
    {
        // Arrange
        var formatter = new FakeFormatter("acp", WebhookFormattedRequest.Fail("The platform's signing secret can't be read."));
        var dispatcher = CreateDispatcher(new WebhookSendResult { Succeeded = true }, formatter);
        var delivery = DirectDelivery("acp");

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(_sent);
        Assert.Equal(WebhookDeliveryStatus.Failed, delivery.Status);
        Assert.Null(delivery.NextAttemptUtc);
        Assert.Equal("The platform's signing secret can't be read.", delivery.LastError);
    }

    [Fact]
    public async Task SendAsync_WhenTheReceiverFails_TriesADirectDeliveryAgainLater()
    {
        // Arrange
        var formatter = new FakeFormatter("acp", new WebhookFormattedRequest());
        var dispatcher = CreateDispatcher(new WebhookSendResult { Succeeded = false, StatusCode = 503, Error = "Unavailable" }, formatter);
        var delivery = DirectDelivery("acp");

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(_sent);
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);
        Assert.Equal(_now + WebhookDispatcher.Backoff[0], delivery.NextAttemptUtc);
        Assert.Equal(503, delivery.LastStatusCode);
    }

    private WebhookDispatcher CreateDispatcher(WebhookSendResult result, params IWebhookDeliveryFormatter[] formatters)
    {
        var sender = new Mock<IWebhookSender>();

        sender
            .Setup(entry => entry.SendAsync(It.IsAny<WebhookSendRequest>(), It.IsAny<CancellationToken>()))
            .Callback<WebhookSendRequest, CancellationToken>((request, _) => _sent.Add(request))
            .ReturnsAsync(result);

        return new WebhookDispatcher(
            sender.Object,
            _deliveries.Object,
            _subscriptions.Object,
            formatters,
            new EphemeralDataProtectionProvider(),
            Mock.Of<IClock>(clock => clock.UtcNow == _now),
            NullLogger<WebhookDispatcher>.Instance);
    }

    private static WebhookDelivery DirectDelivery(string formatterName)
        => new()
        {
            ItemId = "d1",
            EventId = "evt_1",
            EventName = "order_create",
            Payload = "{\"type\":\"order_create\"}",
            Url = "https://agent.example.com/webhooks",
            FormatterName = formatterName,
            FormatterData = new Dictionary<string, string> { ["platformId"] = "platform-1" },
            Status = WebhookDeliveryStatus.Pending,
        };

    private sealed class FakeFormatter : IWebhookDeliveryFormatter
    {
        private readonly WebhookFormattedRequest _result;

        public FakeFormatter(string name, WebhookFormattedRequest result)
        {
            Name = name;
            _result = result;
        }

        public string Name { get; }

        public List<WebhookFormatContext> Contexts { get; } = [];

        public Task<WebhookFormattedRequest> FormatAsync(WebhookFormatContext context, CancellationToken cancellationToken = default)
        {
            Contexts.Add(context);

            return Task.FromResult(_result);
        }
    }
}
