using System.Net;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Sending a caller to voicemail on Telnyx.
/// </summary>
/// <remarks>
/// A ringing inbound call has to be answered before a greeting can be played or a message recorded. A call that
/// is already up does not — and Telnyx refuses the attempt outright with "Can not issue an answer command on an
/// outbound call". That refusal used to abort the whole operation, so a caller who reached voicemail from a queue
/// (already connected, by definition) heard the hold music stop and then nothing at all.
/// </remarks>
public sealed class TelnyxSendToVoicemailTests
{
    [Fact]
    public async Task ACallerAlreadyOnTheLine_IsStillGreetedAndRecorded()
    {
        // Arrange
        // Telnyx refuses the answer, then reports the leg alive. The answer only exists to guarantee a live leg,
        // so a refusal that means the leg is already live is the goal rather than a failure.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90102","title":"Invalid command","detail":"Can not issue an answer command on an outbound call."}]}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"is_alive":true}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");

        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SendToVoicemailAsync(
            new CallReference { CallId = "ctrl-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);

        // It did not stop at the refused answer: it went on to look the call up and then act on it.
        Assert.True(handler.Requests.Count >= 3, $"Only {handler.Requests.Count} request(s) were made; the operation stopped at the refused answer.");
        Assert.Contains(handler.Requests, request => request.Path.Contains("/actions/answer", StringComparison.Ordinal));
        Assert.Contains(handler.Requests, request => request.Method == HttpMethod.Get && request.Path.Contains("/calls/ctrl-1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ALegThatIsAlreadyGone_DoesNotHaveVoicemailPlayedIntoIt()
    {
        // Arrange
        // The other half of the guard. Treating every refused answer as "already live" would leave voicemail
        // greeting a call that has hung up, so the provider is asked rather than the error text interpreted.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"90102","detail":"Can not issue an answer command on an outbound call."}]}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"is_alive":false}}""");

        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SendToVoicemailAsync(
            new CallReference { CallId = "ctrl-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task ARingingCallThatAnswersNormally_IsUnaffected()
    {
        // Arrange
        // The inbound case this path was written for must keep working, and must not pay for an extra lookup.
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");

        var provider = CreateProvider(handler);

        // Act
        var result = await provider.SendToVoicemailAsync(
            new CallReference { CallId = "ctrl-1" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Get);
    }

    private static TelnyxTelephonyProvider CreateProvider(RecordingHttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler)
        {
            BaseAddress = new Uri("https://api.telnyx.com/v2/"),
        };

        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiBaseUrl = "https://api.telnyx.com/v2/",
            ApiKey = "test-api-key",
            ConnectionId = "test-connection",
        };

        var apiClient = new TelnyxApiClient(
            httpClient,
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        var optionsMonitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        optionsMonitor.SetupGet(x => x.CurrentValue).Returns(options);

        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc));

        return new TelnyxTelephonyProvider(
            apiClient,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            new Mock<ITelnyxAgentEndpointResolver>().Object,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            optionsMonitor.Object);
    }
}
