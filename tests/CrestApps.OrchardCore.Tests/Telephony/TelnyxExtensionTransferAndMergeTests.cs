using System.Net;
using System.Text.Json;
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
/// Transferring a Telnyx call to an internal extension, and adding a call to a conference that is already running.
/// </summary>
/// <remarks>
/// An extension is not a number Telnyx can dial: a colleague is reached on the SIP address their browser is registered
/// on, the same address an extension call rings. Handing Telnyx the extension's digits as a destination dialled "2" as a
/// phone number.
/// </remarks>
public sealed class TelnyxExtensionTransferAndMergeTests
{
    private const string ColleagueEndpoint = "sip:gencred2@sip.telnyx.com";

    // A call's status as Telnyx reports it for a call that is not a number dialed from the soft phone.
    private const string NotADialedNumber = """{"data":{"call_control_id":"ctrl-a","is_alive":true}}""";

    [Theory]
    [InlineData(TransferMode.Blind)]
    [InlineData(TransferMode.Warm)]
    public async Task TransferToAnExtension_SendsTheCallToTheColleaguesRegisteredBrowser(TransferMode mode)
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));
        var request = new TransferRequest { CallId = "ctrl-1", To = "2", IsExtension = true, TargetUserId = "user-2", Mode = mode };

        // Act
        var result = mode == TransferMode.Warm
            ? await provider.StartAttendedTransferAsync(request, TestContext.Current.CancellationToken)
            : await provider.TransferAsync(request, TestContext.Current.CancellationToken);

        // Assert - the call is read first, to learn whether it is a number dialed from the soft phone; it is not.
        Assert.True(result.Succeeded);
        Assert.Equal(["/v2/calls/ctrl-1", "/v2/calls/ctrl-1/actions/transfer"], handler.Requests.Select(request => request.Path));
        Assert.Equal(ColleagueEndpoint, ReadString(handler.Requests[1].Body, "to"));
    }

    [Fact]
    public async Task TransferToAnExtensionWhoseBrowserIsNotRegistered_FailsWithoutCallingTelnyx()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler, ResolverFor("user-2", null));

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest { CallId = "ctrl-1", To = "2", IsExtension = true, TargetUserId = "user-2" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Contains("2", result.Error);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransferToAnExtensionNobodyWasResolvedFor_FailsWithoutCallingTelnyx()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler();
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest { CallId = "ctrl-1", To = "2", IsExtension = true },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TransferToANumber_SendsTheNumberAsItIs()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));

        // Act
        var result = await provider.TransferAsync(
            new TransferRequest { CallId = "ctrl-1", To = "+17025550199" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("+17025550199", ReadString(handler.Requests[^1].Body, "to"));
    }

    [Fact]
    public async Task MergeIntoARunningConference_JoinsTheNewCalls_WithoutMakingASecondConference()
    {
        // Arrange - the conference made from ctrl-a is still running.
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, """{"data":[{"id":"conference-1","name":"conf-ctrl-a"}]}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));

        // Act
        var result = await provider.MergeAsync(
            new MergeRequest { CallIds = ["ctrl-a", "ctrl-n", "ctrl-m"], ConferenceName = "conf-ctrl-a" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(
            [
                "GET /v2/calls/ctrl-a",
                "GET /v2/calls/ctrl-n",
                "GET /v2/calls/ctrl-m",
                "GET /v2/conferences?filter[name]=conf-ctrl-a&filter[status]=in_progress",
                "POST /v2/conferences/conference-1/actions/join",
                "POST /v2/conferences/conference-1/actions/join",
            ],
            handler.Requests.Select(request => $"{request.Method} {Uri.UnescapeDataString(request.Path)}"));
        Assert.Equal("ctrl-n", ReadString(handler.Requests[4].Body, "call_control_id"));
        Assert.Equal("ctrl-m", ReadString(handler.Requests[5].Body, "call_control_id"));
        Assert.Equal("conf-ctrl-a", result.Call.Metadata["conferenceName"]);
    }

    [Fact]
    public async Task MergeNamingAConferenceThatHasEnded_MakesItAgain()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, """{"data":[]}""")
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-2"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));

        // Act
        var result = await provider.MergeAsync(
            new MergeRequest { CallIds = ["ctrl-a", "ctrl-n"], ConferenceName = "conf-ctrl-a" },
            TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("POST /v2/conferences", $"{handler.Requests[3].Method} {handler.Requests[3].Path}");
        Assert.Equal("ctrl-a", ReadString(handler.Requests[3].Body, "call_control_id"));
        Assert.Equal("POST /v2/conferences/conference-2/actions/join", $"{handler.Requests[4].Method} {handler.Requests[4].Path}");
    }

    [Fact]
    public async Task MergeWithoutAConferenceName_MakesOneFromTheFirstCall_AndNamesIt()
    {
        // Arrange
        var handler = new RecordingHttpMessageHandler()
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, NotADialedNumber)
            .RespondWith(HttpStatusCode.OK, """{"data":{"id":"conference-3"}}""")
            .AlwaysRespondWith(HttpStatusCode.OK, """{"data":{"result":"ok"}}""");
        var provider = CreateProvider(handler, ResolverFor("user-2", ColleagueEndpoint));

        // Act
        var result = await provider.MergeAsync(new MergeRequest { CallIds = ["ctrl-a", "ctrl-b", "ctrl-c"] }, TestContext.Current.CancellationToken);

        // Assert - one conference, and all three calls in it.
        Assert.True(result.Succeeded);
        Assert.Equal("POST /v2/conferences", $"{handler.Requests[3].Method} {handler.Requests[3].Path}");
        Assert.Equal(2, handler.Requests.Count(request => request.Path.EndsWith("/actions/join", StringComparison.Ordinal)));
        Assert.Equal("conf-ctrl-a", result.Call.Metadata["conferenceName"]);
        Assert.Equal(3, result.Call.Metadata["participantCount"]);
    }

    private static string ReadString(string json, string property)
    {
        using var document = JsonDocument.Parse(json);

        return document.RootElement.GetProperty(property).GetString();
    }

    private static ITelnyxAgentEndpointResolver ResolverFor(string userId, string endpoint)
    {
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();

        resolver
            .Setup(service => service.ResolveAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(endpoint);

        return resolver.Object;
    }

    private static TelnyxTelephonyProvider CreateProvider(RecordingHttpMessageHandler handler, ITelnyxAgentEndpointResolver endpointResolver)
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
            endpointResolver,
            clock.Object,
            NullLogger<TelnyxTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<TelnyxTelephonyProvider>(),
            optionsMonitor.Object);
    }
}
