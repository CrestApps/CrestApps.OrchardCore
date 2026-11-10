using System.Diagnostics;
using System.Net;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Telnyx.Telemetry;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telemetry.VoiceTelephony;

/// <summary>
/// The Telnyx REST client and webhook pipeline emit the instruments and spans the observability docs list under
/// <c>CrestApps.OrchardCore.Telnyx</c>. A request path names the call-control id, so only the endpoint family ever
/// reaches a tag, and the webhook's processing in the inbox is linked back to the delivery that brought it.
/// </summary>
public sealed class TelnyxTelemetryTests
{
    [Theory]
    [InlineData("POST", "calls", "calls.create")]
    [InlineData("GET", "calls?filter[connection_id]=123&page[size]=250", "calls.list")]
    [InlineData("GET", "calls/v3%3Actrl-1", "calls.get")]
    [InlineData("POST", "calls/v3%3Actrl-1/actions/answer", "calls.answer")]
    [InlineData("PUT", "calls/ctrl-1/actions/client_state_update", "calls.client_state_update")]
    [InlineData("POST", "conferences/conf-9/actions/join", "conferences.join")]
    [InlineData("GET", "conferences/conf-9/participants", "conferences.participants")]
    [InlineData("DELETE", "telephony_credentials/cred-1", "credentials.delete")]
    [InlineData("PATCH", "phone_numbers/num-1/voice", "phone_numbers.voice")]
    [InlineData("POST", "calls/ctrl-1/actions/Speak+1234", "calls.other")]
    [InlineData("GET", "recordings/rec-1", "other")]
    [InlineData("GET", "", "other")]
    public void GetApiOperation_DropsIdsAndKeepsTheEndpointFamily(string method, string path, string expected)
    {
        // Act
        var operation = TelnyxTelemetry.GetApiOperation(new HttpMethod(method), path);

        // Assert
        Assert.Equal(expected, operation);
    }

    [Theory]
    [InlineData("call.answered", "call.answered")]
    [InlineData(" CALL.HANGUP ", "call.hangup")]
    [InlineData("message.received", "message.received")]
    [InlineData("call.something.new", "other")]
    [InlineData(null, "other")]
    public void GetWebhookType_KeepsKnownTypesAndBoundsTheRest(string eventType, string expected)
    {
        // Act
        var type = TelnyxTelemetry.GetWebhookType(eventType);

        // Assert
        Assert.Equal(expected, type);
    }

    [Fact]
    public async Task SendAsync_RecordsTheRequestDurationTaggedWithTheOperationAndNeverTheCallControlId()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        // An action name of lower-case letters only, as Telnyx's own are, so it is kept as the operation; unique to
        // this test, because the meters are process-wide and tests run in parallel.
        var action = "telemetry_" + string.Concat(Guid.NewGuid().ToString("n")[..12].Select(digit => (char)('a' + Convert.ToInt32(digit.ToString(), 16))));
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.PostCallActionAsync("v3:secret-call-control-id", action, body: null, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        var measurement = Assert.Single(capture.Find("crestapps.telnyx.api.request.duration", CrestAppsOrchardCoreTelemetry.OperationTag, "calls." + action));
        Assert.Equal(TelnyxTelemetry.SourceName, measurement.Meter);
        Assert.Equal(TelnyxTelemetry.ProviderName, measurement.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]);
        Assert.Equal("2xx", measurement.Tags[CrestAppsOrchardCoreTelemetry.StatusClassTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, measurement.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.False((bool)measurement.Tags[TelnyxTelemetry.RetriedTag]);
        Assert.DoesNotContain(measurement.Tags.Values, value => value is string text && text.Contains("secret-call-control-id", StringComparison.Ordinal));
        Assert.Empty(capture.FindBannedTagKeys());

        var span = Assert.Single(capture.Spans, activity =>
            activity.OperationName == "crestapps.telnyx.api.request" &&
            Equals(activity.GetTagItem(CrestAppsOrchardCoreTelemetry.OperationTag), "calls." + action));
        Assert.Equal(ActivityKind.Client, span.Kind);
        Assert.Equal(200, span.GetTagItem("http.response.status_code"));
        Assert.DoesNotContain(span.TagObjects, tag => tag.Value is string text && text.Contains("secret-call-control-id", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SendAsync_WhenARetryableCommandIsRetried_TagsTheRequestAsRetriedAndCountsTheRetry()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        using var parent = new Activity("telnyx-retry-test").Start();
        var traceId = parent.TraceId;
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.ServiceUnavailable)
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-retry", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);

        var span = Assert.Single(capture.Spans, activity => activity.OperationName == "crestapps.telnyx.api.request" && activity.TraceId == traceId);
        Assert.True((bool)span.GetTagItem(TelnyxTelemetry.RetriedTag));
        Assert.Equal("calls.answer", span.GetTagItem(CrestAppsOrchardCoreTelemetry.OperationTag));

        Assert.Contains(capture.Find("crestapps.retry.attempts", CrestAppsOrchardCoreTelemetry.AreaTag, "telnyx"), measurement =>
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag], "status_5xx") && Equals(measurement.Tags["attempt"], 2));
    }

    [Fact]
    public async Task ProcessWebhookAsync_LinksTheProcessingSpanToTheDeliveryThatQueuedIt()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var eventId = "evt-" + Guid.NewGuid().ToString("n");
        var webhookService = new Mock<ITelnyxWebhookService>();
        webhookService
            .Setup(service => service.ProcessAsync(It.IsAny<TelnyxCallEvent>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(TelnyxWebhookResult.Updated);

        string traceParent;
        ActivityTraceId deliveryTraceId;

        // The delivery's span, as the webhook endpoint holds it while it stores the event in the inbox.
        using (var delivery = new Activity("telnyx-delivery").SetIdFormat(ActivityIdFormat.W3C).Start())
        {
            deliveryTraceId = delivery.TraceId;
            traceParent = TraceContextPropagation.Capture();
        }

        var callEvent = new TelnyxCallEvent
        {
            EventId = eventId,
            EventType = "call.answered",
            CallControlId = "ctrl-linked",
            TraceParent = traceParent,
        };

        // Act
        var result = await TelnyxTelemetry.ProcessWebhookAsync(webhookService.Object, callEvent, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, result);

        var span = Assert.Single(capture.Spans, activity =>
            activity.OperationName == "crestapps.telnyx.webhook.process" &&
            Equals(activity.GetTagItem("crestapps.telnyx.event.id"), eventId));
        Assert.NotEqual(deliveryTraceId, span.TraceId);
        Assert.Contains(span.Links, link => link.Context.TraceId == deliveryTraceId);
        Assert.Equal("call.answered", span.GetTagItem(TelnyxTelemetry.WebhookTypeTag));
        Assert.Equal("updated", span.GetTagItem(CrestAppsOrchardCoreTelemetry.ResultTag));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    private static TelnyxApiClient CreateClient(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var options = new TelnyxOptions
        {
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
        };

        return new TelnyxApiClient(
            httpClient,
            new TestOptionsMonitor<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);
    }
}
