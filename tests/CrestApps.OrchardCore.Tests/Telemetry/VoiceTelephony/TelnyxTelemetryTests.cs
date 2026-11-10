using System.Net;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Telnyx.Telemetry;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telemetry.VoiceTelephony;

/// <summary>
/// The Telnyx webhook pipeline emits the instruments the observability docs list under <c>CrestApps.OrchardCore.Telnyx</c>,
/// and the REST client counts its own retries. The HTTP requests themselves are measured by the HttpClient
/// instrumentation, not here.
/// </summary>
public sealed class TelnyxTelemetryTests
{
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
    public async Task SendAsync_WhenARetryableCommandIsRetried_CountsTheRetry()
    {
        // Arrange
        using var capture = new TelemetryCapture();
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.ServiceUnavailable)
            .RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var client = CreateClient(handler);

        // Act
        var result = await client.AnswerAsync("ctrl-retry", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);

        Assert.Contains(capture.Find("crestapps.retry.attempts", CrestAppsOrchardCoreTelemetry.AreaTag, "telnyx"), measurement =>
            Equals(measurement.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag], "status_5xx") && Equals(measurement.Tags["attempt"], 2));
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
