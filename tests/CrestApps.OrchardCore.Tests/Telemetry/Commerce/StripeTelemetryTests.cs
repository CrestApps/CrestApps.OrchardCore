using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core.Telemetry;
using CrestApps.OrchardCore.Stripe.Endpoints;
using Moq;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Telemetry.Commerce;

/// <summary>
/// The Stripe integration reports webhook handling on the <c>CrestApps.OrchardCore.Stripe</c> meter with bounded tags:
/// an event type the webhook does not handle is <c>other</c>.
/// </summary>
public sealed class StripeTelemetryTests
{
    [Theory]
    [InlineData(EventTypes.InvoicePaymentSucceeded, EventTypes.InvoicePaymentSucceeded)]
    [InlineData(EventTypes.ChargeDisputeClosed, EventTypes.ChargeDisputeClosed)]
    [InlineData("customer.created", "other")]
    [InlineData("evt_attacker_controlled_value", "other")]
    [InlineData(null, "unknown")]
    [InlineData("", "unknown")]
    public void GetEventTypeTag_KeepsOnlyTheTypesTheWebhookHandles(string eventType, string expected)
    {
        // Act
        var tag = CreateWebhookEndpoint.GetEventTypeTag(eventType);

        // Assert
        Assert.Equal(expected, tag);
    }

    [Fact]
    public async Task DispatchAsync_RecordsTheHandlerTimeByBoundedEventType_AndPutsTheEventIdOnTheSpanOnly()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var eventId = "evt_" + Guid.NewGuid().ToString("n");
        var stripeEvent = new Event
        {
            Id = eventId,
            Type = "customer.created",
        };

        // Act
        await CreateWebhookEndpoint.DispatchAsync(stripeEvent, [], TestContext.Current.CancellationToken);

        // Assert
        var span = Assert.Single(capture.Spans, candidate => Equals(candidate.GetTagItem(StripeTelemetry.EventIdTag), eventId));
        Assert.Equal("crestapps.stripe.webhook.dispatch", span.OperationName);
        Assert.Equal("other", span.GetTagItem(StripeTelemetry.EventTypeTag));

        Assert.Contains(
            capture.Find("crestapps.stripe.webhook.handler.duration", StripeTelemetry.EventTypeTag, "other"),
            measurement => Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.OutcomeTag), CrestAppsOrchardCoreTelemetry.OutcomeOk));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, eventId)));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public async Task DispatchAsync_WhenAHandlerFails_RecordsAnError()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var eventId = "evt_" + Guid.NewGuid().ToString("n");
        var handler = new Mock<IPaymentEvent>();
        handler
            .Setup(paymentEvent => paymentEvent.PaymentIntentSucceededAsync(It.IsAny<PaymentIntentSucceededContext>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("The handler failed."));

        var stripeEvent = new Event
        {
            Id = eventId,
            Type = EventTypes.PaymentIntentSucceeded,
            Data = new EventData
            {
                Object = new PaymentIntent
                {
                    Id = "pi_1",
                    Amount = 2000,
                    Currency = "usd",
                    Metadata = [],
                },
            },
        };

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => CreateWebhookEndpoint.DispatchAsync(stripeEvent, [handler.Object], TestContext.Current.CancellationToken));

        // Assert
        var span = Assert.Single(capture.Spans, candidate => Equals(candidate.GetTagItem(StripeTelemetry.EventIdTag), eventId));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, span.GetTagItem(CrestAppsOrchardCoreTelemetry.OutcomeTag));
        Assert.Contains(
            capture.Find("crestapps.stripe.webhook.handler.duration", StripeTelemetry.EventTypeTag, EventTypes.PaymentIntentSucceeded),
            measurement => Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.OutcomeTag), CrestAppsOrchardCoreTelemetry.OutcomeError));
        Assert.Empty(capture.FindBannedTagKeys());
    }
}
