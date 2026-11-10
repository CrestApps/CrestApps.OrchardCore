using System.Net;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Payments;
using CrestApps.OrchardCore.Stripe.Core.Telemetry;
using CrestApps.OrchardCore.Stripe.Endpoints;
using CrestApps.OrchardCore.Stripe.Services;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using OrchardCore.Environment.Shell;
using Stripe;

namespace CrestApps.OrchardCore.Tests.Telemetry.Commerce;

/// <summary>
/// The Stripe integration reports webhook handling and API calls on the <c>CrestApps.OrchardCore.Stripe</c> meter with
/// bounded tags: an event type the webhook does not handle is <c>other</c>, and an API call is tagged with its resource,
/// never with the ids in its path.
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

    [Theory]
    [InlineData("https://api.stripe.com/v1/payment_intents/pi_123/confirm", "payment_intents")]
    [InlineData("https://api.stripe.com/v1/customers/cus_123", "customers")]
    [InlineData("https://api.stripe.com/v1/refunds", "refunds")]
    [InlineData("https://api.stripe.com/v1/pi_123", "other")]
    [InlineData("https://api.stripe.com/v1", "other")]
    public void GetApiOperation_TagsTheResourceNeverTheIds(string uri, string expected)
    {
        // Act
        var operation = StripeTelemetry.GetApiOperation(new Uri(uri));

        // Assert
        Assert.Equal(expected, operation);
    }

    [Theory]
    [InlineData(200, "ok")]
    [InlineData(402, StripeTelemetry.ApiCardDeclined)]
    [InlineData(429, StripeTelemetry.ApiRateLimited)]
    [InlineData(401, StripeTelemetry.ApiAuthenticationError)]
    [InlineData(403, StripeTelemetry.ApiAuthenticationError)]
    [InlineData(404, StripeTelemetry.ApiInvalidRequest)]
    [InlineData(503, StripeTelemetry.ApiError)]
    public void GetApiOutcome_MapsTheStatusCode(int statusCode, string expected)
    {
        // Act
        var outcome = StripeTelemetry.GetApiOutcome(statusCode);

        // Assert
        Assert.Equal(expected, outcome);
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
        AssertNoBannedTags(capture);
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
        AssertNoBannedTags(capture);
    }

    [Fact]
    public async Task StripeApiFailureHandler_RecordsADeclinedCardByResource()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        using var handler = new StripeApiFailureHandler(memoryCache, new ShellSettings { Name = "Default" })
        {
            InnerHandler = new StatusCodeHandler(HttpStatusCode.PaymentRequired),
        };
        using var invoker = new HttpMessageInvoker(handler);
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.stripe.com/v1/payment_intents/pi_123/confirm");

        // Act
        using var response = await invoker.SendAsync(request, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);

        var requests = capture.Find("crestapps.stripe.api.requests", CrestAppsOrchardCoreTelemetry.OperationTag, "payment_intents");
        Assert.Contains(requests, measurement =>
            Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.OutcomeTag), StripeTelemetry.ApiCardDeclined) &&
            Equals(measurement.Tags.GetValueOrDefault(CrestAppsOrchardCoreTelemetry.StatusClassTag), "4xx"));
        Assert.NotEmpty(capture.Find("crestapps.stripe.api.duration", CrestAppsOrchardCoreTelemetry.OperationTag, "payment_intents"));
        Assert.DoesNotContain(capture.Measurements, measurement => measurement.Tags.Values.Any(value => Equals(value, "pi_123")));
        AssertNoBannedTags(capture);
    }

    private static void AssertNoBannedTags(TelemetryCapture capture)
    {
        var banned = capture.Measurements
            .Where(measurement => measurement.Meter == StripeTelemetry.SourceName)
            .SelectMany(measurement => measurement.Tags.Keys)
            .Where(CrestAppsOrchardCoreTelemetry.BannedMetricTagKeys.Contains)
            .ToList();

        Assert.Empty(banned);
    }

    private sealed class StatusCodeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;

        public StatusCodeHandler(HttpStatusCode statusCode)
            => _statusCode = statusCode;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(new HttpResponseMessage(_statusCode));
    }
}
