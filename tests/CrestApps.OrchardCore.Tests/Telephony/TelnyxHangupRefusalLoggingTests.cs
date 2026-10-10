using System.Net;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A hangup Telnyx refuses because the call has already ended got what it asked for: the customer put the phone down
/// first. It was logged as a warning at the end of nearly every call the customer finished.
/// </summary>
public sealed class TelnyxHangupRefusalLoggingTests
{
    private const string CallAlreadyEnded = """
        { "errors": [ { "code": "90018", "title": "Call has already ended", "detail": "This call is no longer active and can't receive commands." } ] }
        """;

    [Fact]
    public async Task AHangupOfACallThatHasAlreadyEnded_IsNotAWarning()
    {
        // Arrange
        var logger = new RecordingLogger<TelnyxApiClient>();
        var client = CreateClient(new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, CallAlreadyEnded), logger);

        // Act
        var result = await client.HangupAsync("call-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(logger.At(LogLevel.Warning));
    }

    [Fact]
    public async Task AnyOtherCommandOnACallThatHasEnded_IsStillAWarning()
    {
        // Arrange
        // Answering, speaking or bridging a call that is gone is a real failure of whatever wanted it done.
        var logger = new RecordingLogger<TelnyxApiClient>();
        var client = CreateClient(new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, CallAlreadyEnded), logger);

        // Act
        await client.AnswerAsync("call-1", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(logger.At(LogLevel.Warning));
    }

    private static TelnyxApiClient CreateClient(HttpMessageHandler handler, ILogger<TelnyxApiClient> logger)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        return new TelnyxApiClient(
            httpClient,
            new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions
            {
                ApiBaseUrl = "https://api.telnyx.com/v2/",
                ApiKey = "test-api-key",
            }),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            logger);
    }
}
