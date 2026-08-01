using System.Net;
using System.Text.Json;
using CrestApps.OrchardCore.Asterisk;
using CrestApps.OrchardCore.Asterisk.Models;
using CrestApps.OrchardCore.Asterisk.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using CrestApps.OrchardCore.Tests.Telephony.ProviderContracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Proves the Asterisk <see cref="ITelephonyCallStateProvider"/> — the surface reconciliation relies on to recover the
/// telephony server's truth — issues a live Asterisk REST Interface channel lookup and parses the recorded channel the
/// pinned release publishes. A provider that stops querying live ARI (for example one that starts trusting local call
/// state) issues no channel request and fails these tests, so "reconciliation" that quietly reads local state cannot
/// pass for reconciliation against the provider.
/// </summary>
public sealed class AsteriskCallStateReconciliationContractTests
{
    private const string BaseUrl = "http://asterisk.contract.invalid/ari/";

    [Fact]
    public async Task GetCallStateAsync_IssuesLiveAriChannelLookup_AndParsesTheRecordedChannel()
    {
        // Arrange
        var cassettes = AsteriskContractCassettes.Load();

        Assert.True(
            cassettes.TryReadRecordedRestResponse("GET", "channels/{channelId}", out var statusCode, out var channelBody),
            "The Asterisk cassette must record a channel lookup response so the reconciliation liveness proof has a live payload to replay.");

        using var recordedChannel = JsonDocument.Parse(channelBody);
        var channelId = recordedChannel.RootElement.GetProperty("id").GetString();

        var handler = new StubHttpMessageHandler(request => Respond(request, channelId, (HttpStatusCode)statusCode, channelBody));
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.GetCallStateAsync(channelId, TestContext.Current.CancellationToken);

        // Assert — the provider hit live ARI for the channel and produced provider truth from the recorded payload.
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri.AbsolutePath.EndsWith($"/channels/{channelId}", StringComparison.Ordinal));

        Assert.True(result.Succeeded);
        Assert.True(result.Found);
        Assert.NotNull(result.Call);
        Assert.Equal(CallState.Connected, result.Call.State);

        // The recorded caller and connected numbers differ, so mapping them to From/To pins the direction.
        Assert.Equal(
            recordedChannel.RootElement.GetProperty("caller").GetProperty("number").GetString(),
            result.Call.From);
        Assert.Equal(
            recordedChannel.RootElement.GetProperty("connected").GetProperty("number").GetString(),
            result.Call.To);
    }

    [Fact]
    public async Task GetCallStateAsync_WhenLiveAriNoLongerKnowsTheChannel_ReconcilesToNotFound()
    {
        // Arrange — a 404 from live ARI is the truth that a call the tenant still tracks has already ended.
        var cassettes = AsteriskContractCassettes.Load();

        Assert.True(cassettes.TryReadRecordedRestResponse("GET", "channels/{channelId}", out _, out var channelBody));

        using var recordedChannel = JsonDocument.Parse(channelBody);
        var channelId = recordedChannel.RootElement.GetProperty("id").GetString();

        var handler = new StubHttpMessageHandler(HttpStatusCode.NotFound);
        var provider = CreateProvider(handler);

        // Act
        var result = await provider.GetCallStateAsync(channelId, TestContext.Current.CancellationToken);

        // Assert — the not-found truth still comes from a live channel query, not from local state.
        Assert.Contains(handler.Requests, request =>
            request.Method == HttpMethod.Get &&
            request.RequestUri.AbsolutePath.EndsWith($"/channels/{channelId}", StringComparison.Ordinal));

        Assert.True(result.Succeeded);
        Assert.False(result.Found);
        Assert.Null(result.Call);
    }

    [Fact]
    public void AsteriskProvider_ImplementsTheCallStateContract_SoReconciliationCanQueryIt()
    {
        // Reconciliation dispatches on a runtime `is ITelephonyCallStateProvider` check, so if the Asterisk provider
        // stopped implementing it the reconciliation pass would silently skip live ARI for every call.
        Assert.True(typeof(ITelephonyCallStateProvider).IsAssignableFrom(typeof(AsteriskTelephonyProvider)));
    }

    private static HttpResponseMessage Respond(
        HttpRequestMessage request,
        string channelId,
        HttpStatusCode channelStatusCode,
        string channelBody)
    {
        var path = request.RequestUri.AbsolutePath;

        // Channel-variable lookups enrich hold/mute/conference state; the reconciliation truth is the channel itself.
        if (path.Contains("/variable", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"value":""}"""),
            };
        }

        if (path.EndsWith($"/channels/{channelId}", StringComparison.Ordinal))
        {
            return new HttpResponseMessage(channelStatusCode)
            {
                Content = new StringContent(channelBody),
            };
        }

        return new HttpResponseMessage(HttpStatusCode.NotImplemented);
    }

    private static AsteriskTelephonyProvider CreateProvider(StubHttpMessageHandler handler)
    {
        var dataProtectionProvider = new EphemeralDataProtectionProvider();
        var settings = new AsteriskSettings
        {
            IsEnabled = true,
            BaseUrl = BaseUrl,
            UserName = "ari-user",
            Password = dataProtectionProvider.CreateProtector(AsteriskConstants.ProtectorName).Protect("secret"),
            ApplicationName = "crestapps-telephony",
            TimeoutSeconds = 30,
        };

        var shellSettings = new ShellSettings { Name = "Default" };
        var gate = new AsteriskAriApplicationGate(
            new AsteriskAriApplicationOwnershipRegistry(NullLogger<AsteriskAriApplicationOwnershipRegistry>.Instance),
            shellSettings,
            Options.Create(new DefaultAsteriskOptions()));

        return new AsteriskTelephonyProvider(
            SiteServiceFactory.Create(settings),
            dataProtectionProvider,
            new StubHttpClientFactory(handler),
            gate,
            new StubClock(),
            RedactorProviderFactory.Create(),
            NullLogger<AsteriskTelephonyProvider>.Instance,
            new PassThroughStringLocalizer<AsteriskTelephonyProvider>());
    }
}
