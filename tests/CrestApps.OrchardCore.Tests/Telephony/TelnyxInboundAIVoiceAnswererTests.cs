using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// An entry point that routes a number to an AI voice agent answers the call with the AI voice leg's client state, and
/// Telnyx echoes that state on every later event of the call. The echoed value is how the answered event finds the
/// conversation, so it has to come back as something the webhook can read.
/// </summary>
public sealed class TelnyxInboundAIVoiceAnswererTests
{
    [Fact]
    public async Task AnsweringForTheAIAgent_SendsAClientStateTheAnsweredEventCanRead()
    {
        // Arrange
        // The API client base64-encodes the client state itself. Handing it a state that was already encoded made
        // Telnyx echo a value that decodes to base64 rather than to JSON: the answered event matched no AI voice leg,
        // the conversation never started, and the caller heard silence until they hung up.
        var handler = new RecordingHttpMessageHandler().RespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var answerer = new TelnyxInboundAIVoiceAnswerer(CreateClient(handler));

        // Act
        var answered = await answerer.AnswerAsync("ctrl-1", "activity-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(answered);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v2/calls/ctrl-1/actions/answer", request.Path);

        using var body = JsonDocument.Parse(request.Body);
        var echoedClientState = body.RootElement.GetProperty("client_state").GetString();

        Assert.True(TelnyxOutboundBridgeState.TryParseEncoded(echoedClientState, out var state));
        Assert.Equal(TelnyxOutboundBridgeState.AiVoiceLegIntent, state.Intent);
        Assert.Equal("activity-1", state.ActivityId);
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
