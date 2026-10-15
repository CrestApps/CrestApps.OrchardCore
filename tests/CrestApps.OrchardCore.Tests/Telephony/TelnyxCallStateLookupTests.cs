using System.Net;
using CrestApps.OrchardCore.Telnyx.Models;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Reading the state of a Telnyx call the provider no longer knows.
/// </summary>
/// <remarks>
/// The sweep that settles interactions asks Telnyx whether each call is still up. Telnyx answers a call control id older
/// than the call it named with a 422 (90015 "Invalid Call Control ID", or 90018 "Call has already ended") rather than a
/// 404. Read as "could not query", the interaction behind it was retried every minute and never settled, so the agent it
/// belonged to stayed on a call that was over.
/// </remarks>
public sealed class TelnyxCallStateLookupTests
{
    private const string InvalidCallControlIdBody = """{"errors":[{"code":"90015","title":"Invalid Call Control ID","detail":"The call_control_id provided was not valid."}]}""";
    private const string AlreadyEndedBody = """{"errors":[{"code":"90018","title":"Call has already ended","detail":"This call is no longer active and can't receive commands."}]}""";

    [Theory]
    [InlineData(HttpStatusCode.NotFound, """{"errors":[{"code":"10005","title":"Resource not found"}]}""")]
    [InlineData(HttpStatusCode.UnprocessableEntity, InvalidCallControlIdBody)]
    [InlineData(HttpStatusCode.UnprocessableEntity, AlreadyEndedBody)]
    public async Task ACallTelnyxNoLongerKnows_IsReportedAsNotFound_SoItCanBeSettled(HttpStatusCode status, string body)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(status, body);
        var provider = CreateProvider(handler);

        // Act
        var lookup = await provider.GetCallStateAsync("v3:ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(lookup.Succeeded);
        Assert.False(lookup.Found);
    }

    [Fact]
    public async Task ARefusalThatSaysNothingAboutTheCall_IsStillAFailedLookup()
    {
        // Arrange
        // Any other 422 says nothing about whether the call is up, so nothing may be settled on it.
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.UnprocessableEntity, """{"errors":[{"code":"10015","title":"Bad request"}]}""");
        var provider = CreateProvider(handler);

        // Act
        var lookup = await provider.GetCallStateAsync("v3:ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.False(lookup.Succeeded);
    }

    [Fact]
    public async Task ALiveCall_IsReportedAsFoundAndConnected()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"call_control_id":"v3:ctrl-1","is_alive":true}}""");
        var provider = CreateProvider(handler);

        // Act
        var lookup = await provider.GetCallStateAsync("v3:ctrl-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(lookup.Succeeded);
        Assert.True(lookup.Found);
        Assert.Equal(CrestApps.OrchardCore.Telephony.Models.CallState.Connected, lookup.Call?.State);
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
            new TestOptionsMonitor<TelnyxOptions>(options),
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
