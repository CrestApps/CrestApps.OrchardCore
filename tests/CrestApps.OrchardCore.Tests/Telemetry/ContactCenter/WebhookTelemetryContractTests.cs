using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Webhooks;
using CrestApps.OrchardCore.Webhooks.Core;
using CrestApps.OrchardCore.Webhooks.Core.Models;
using CrestApps.OrchardCore.Webhooks.Core.Services;
using CrestApps.OrchardCore.Webhooks.Core.Telemetry;
using CrestApps.OrchardCore.Webhooks.Models;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telemetry.ContactCenter;

/// <summary>
/// Outbound webhook deliveries and the provider webhook inbox emit the instruments the docs list, on the
/// <c>CrestApps.OrchardCore.Webhooks</c> meter, without a URL, host or delivery id on any metric.
/// </summary>
public sealed class WebhookTelemetryContractTests
{
    private static readonly DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public async Task SendAsync_WhenTheSubscriberFails_CountsARetry()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var delivery = new WebhookDelivery
        {
            ItemId = "delivery-1",
            SubscriptionId = "subscription-1",
            EventId = "evt_1",
            EventName = "order.paid",
            Payload = "{}",
            Status = WebhookDeliveryStatus.Pending,
        };
        var sender = new Mock<IWebhookSender>();
        sender
            .Setup(service => service.SendAsync(It.IsAny<WebhookSendRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WebhookSendResult { StatusCode = 503, Error = "503 Service Unavailable" });
        var dispatcher = CreateDispatcher(sender.Object, delivery.SubscriptionId);

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(WebhookDeliveryStatus.Pending, delivery.Status);

        var attempt = Assert.Single(capture.Find("crestapps.webhook.deliveries"));
        Assert.Equal(WebhookTelemetry.DeliveryRetry, attempt.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("status_5xx", attempt.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag]);
        Assert.Equal("5xx", attempt.Tags[CrestAppsOrchardCoreTelemetry.StatusClassTag]);

        // The HTTP request itself is the HttpClient instrumentation's to measure, not the dispatcher's.
        Assert.DoesNotContain(capture.Spans, span => span.OperationName == "crestapps.webhook.delivery.send");
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task SendAsync_WhenTheWebhookWasDeleted_CountsAFailureWithoutSending()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var delivery = new WebhookDelivery { ItemId = "delivery-2", SubscriptionId = "missing", Status = WebhookDeliveryStatus.Pending };
        var sender = new Mock<IWebhookSender>(MockBehavior.Strict);
        var dispatcher = CreateDispatcher(sender.Object, "another-subscription");

        // Act
        await dispatcher.SendAsync(delivery, TestContext.Current.CancellationToken);

        // Assert
        var attempt = Assert.Single(capture.Find("crestapps.webhook.deliveries"));
        Assert.Equal(WebhookTelemetry.DeliveryError, attempt.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("deleted", attempt.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag]);
        Assert.Equal("none", attempt.Tags[CrestAppsOrchardCoreTelemetry.StatusClassTag]);
    }

    [Fact]
    public void Inbox_RecordsAcceptanceDispatchAndDeadLettersWithBoundedTags()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();

        // Act
        WebhookTelemetry.RecordInboxAccepted("Telnyx", "duplicate");
        WebhookTelemetry.RecordInboxDispatch(activity: null, "retry", "completed");
        WebhookTelemetry.RecordInboxHandler("ContactCenter.TelnyxVoice", TimeSpan.FromMilliseconds(20), exception: null);
        WebhookTelemetry.RecordInboxDeadLettered("Telnyx", "ContactCenter.TelnyxVoice");

        // Assert
        var accepted = Assert.Single(capture.Find("crestapps.webhook.inbox.accepted"));
        Assert.Equal("Telnyx", accepted.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]);
        Assert.Equal("duplicate", accepted.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);

        var dispatch = Assert.Single(capture.Find("crestapps.webhook.inbox.dispatches"));
        Assert.Equal("retry", dispatch.Tags["delivery"]);
        Assert.Equal("completed", dispatch.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);

        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, Assert.Single(capture.Find("crestapps.webhook.inbox.handler.duration")).Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("ContactCenter.TelnyxVoice", Assert.Single(capture.Find("crestapps.webhook.inbox.dead_lettered")).Tags["handler"]);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    private static WebhookDispatcher CreateDispatcher(IWebhookSender sender, string subscriptionId)
    {
        var protection = new EphemeralDataProtectionProvider();
        var subscription = new WebhookSubscription
        {
            ItemId = subscriptionId,
            Url = "https://hooks.example.com/in",
            Events = [WebhookEvents.All],
            IsEnabled = true,
            ProtectedSecret = protection.CreateProtector(WebhookStorage.SecretProtectionPurpose).Protect("shh"),
        };
        var subscriptions = new Mock<IWebhookSubscriptionStore>();
        subscriptions.Setup(store => store.FindByIdAsync(subscription.ItemId, It.IsAny<CancellationToken>())).ReturnsAsync(subscription);

        return new WebhookDispatcher(
            sender,
            Mock.Of<IWebhookDeliveryStore>(),
            subscriptions.Object,
            [],
            protection,
            Mock.Of<IClock>(clock => clock.UtcNow == _now),
            NullLogger<WebhookDispatcher>.Instance);
    }
}
