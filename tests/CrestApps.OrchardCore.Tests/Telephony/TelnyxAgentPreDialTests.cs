using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// The Telnyx half of ringing the agent while the offer is still ringing: what the pre-dialed leg carries, when it is
/// not placed at all, what its webhooks are handed to, and the caller's hold music ending when the agent is joined.
/// </summary>
public sealed class TelnyxAgentPreDialTests
{
    [Fact]
    public async Task PreDialAgentAsync_RingsTheAgentWithTheOfferInTheClientStateAndASipHeader()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"leg-1\"}}");
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveAsync("u1", TelephonyConstants.SoftPhoneClientCapabilities.HeldOfferLeg, It.IsAny<CancellationToken>()))
            .ReturnsAsync("sip:agent@sip.telnyx.com");
        var provider = CreateProvider(handler, resolver.Object);

        // Act
        var result = await provider.PreDialAgentAsync(new ContactCenterAgentPreDialRequest
        {
            ReservationId = "r1",
            ProviderCallId = "caller-1",
            AgentUserId = "u1",
            TimeoutSeconds = 25,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("leg-1", result.ProviderLegId);
        Assert.Equal(VoiceCallState.Dialing, result.ProviderLegState);

        using var body = JsonDocument.Parse(handler.LastRequestBody);
        var root = body.RootElement;
        Assert.Equal("sip:agent@sip.telnyx.com", root.GetProperty("to").GetString());
        Assert.Equal(25, root.GetProperty("timeout_secs").GetInt32());

        var header = root.GetProperty("custom_headers")[0];
        Assert.Equal(TelnyxConstants.OfferIdSipHeader, header.GetProperty("name").GetString());
        Assert.Equal("r1", header.GetProperty("value").GetString());

        var state = Encoding.UTF8.GetString(Convert.FromBase64String(root.GetProperty("client_state").GetString()));
        Assert.True(TelnyxOutboundBridgeState.TryParse(state, out var parsed));
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent, parsed.Intent);
        Assert.Equal("r1", parsed.ReservationId);
        Assert.Equal("caller-1", parsed.PeerCallControlId);
    }

    [Fact]
    public async Task PreDialAgentAsync_WhenNoClientThatCanHoldTheLegIsRegistered_PlacesNoCall()
    {
        // Arrange
        // An older client (a cached page, a browser extension that has not updated) never reported the capability,
        // so it would ring the leg as a second call or answer it on arrival.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{\"call_control_id\":\"leg-1\"}}");
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveAsync("u1", It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string)null);
        var provider = CreateProvider(handler, resolver.Object);

        // Act
        var result = await provider.PreDialAgentAsync(new ContactCenterAgentPreDialRequest
        {
            ReservationId = "r1",
            ProviderCallId = "caller-1",
            AgentUserId = "u1",
            TimeoutSeconds = 25,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ConnectToAgentAsync_WithAPreDialedLeg_ReadiesTheCallerWithoutRingingTheAgentAgain()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        var provider = CreateProvider(handler, resolver.Object);

        // Act
        var result = await provider.ConnectToAgentAsync(new ContactCenterConnectRequest
        {
            ProviderCallId = "caller-1",
            AgentUserId = "u1",
            PreDialedAgentLegId = "leg-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal("leg-1", result.ProviderLegId);
        Assert.Single(handler.Requests);
        Assert.EndsWith("calls/caller-1/actions/answer", handler.Requests[0].RequestUri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BridgePreDialedAgentAsync_StopsTheCallersHoldMusicOnceTheAgentIsJoined()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var provider = CreateProvider(handler, new Mock<ITelnyxAgentEndpointResolver>().Object);

        // Act
        var result = await provider.BridgePreDialedAgentAsync("caller-1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
        Assert.Equal(2, handler.Requests.Count);
        Assert.EndsWith("calls/leg-1/actions/bridge", handler.Requests[0].RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("calls/caller-1/actions/playback_stop", handler.Requests[1].RequestUri.AbsolutePath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BridgePreDialedAgentAsync_WhenNothingIsPlaying_StillSucceeds()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(request => new HttpResponseMessage(
            request.RequestUri.AbsolutePath.EndsWith("playback_stop", StringComparison.Ordinal)
                ? HttpStatusCode.UnprocessableEntity
                : HttpStatusCode.OK)
        {
            Content = new StringContent("{\"data\":{}}"),
        });
        var provider = CreateProvider(handler, new Mock<ITelnyxAgentEndpointResolver>().Object);

        // Act
        var result = await provider.BridgePreDialedAgentAsync("caller-1", "leg-1", TestContext.Current.CancellationToken);

        // Assert
        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Capabilities_SayTheHoldMusicStopsWhenTheAgentIsJoined()
    {
        var provider = CreateProvider(new StubHttpMessageHandler(HttpStatusCode.OK), new Mock<ITelnyxAgentEndpointResolver>().Object);

        Assert.True(provider.Capabilities.HasFlag(ContactCenterVoiceProviderCapabilities.HoldMusicStopsOnAgentBridge));
    }

    [Fact]
    public async Task AdvanceAsync_WhenAPreDialedLegIsAnswered_HandsTheDecisionToTheCoordinator()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var failureService = new Mock<IContactCenterAgentLegFailureService>(MockBehavior.Strict);
        var orchestrator = CreateOrchestrator(handler, failureService.Object, coordinator.Object);

        // Act
        var leg = await orchestrator.AdvanceAsync(PreDialedLegEvent("call.answered"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(TelnyxOutboundBridgeLeg.None, leg);
        coordinator.Verify(value => value.OnAgentLegAnsweredAsync("Telnyx", "r1", "leg-1", It.IsAny<CancellationToken>()), Times.Once);

        // Answering proves only that the browser picked up: nothing is bridged here.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AdvanceAsync_WhenAPreDialedLegEnds_IsNotTreatedAsAFailedConnectByItself()
    {
        // Arrange
        // A declined or expired offer's leg is hung up on purpose; only the coordinator knows whether that matters.
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        var failureService = new Mock<IContactCenterAgentLegFailureService>(MockBehavior.Strict);
        var orchestrator = CreateOrchestrator(handler, failureService.Object, coordinator.Object);

        // Act
        await orchestrator.AdvanceAsync(PreDialedLegEvent("call.hangup", "CALL_REJECTED"), TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(
            value => value.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.Rejected, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AdvanceAsync_WhenAnAcceptTimeAgentLegIsBridged_StopsTheCallersHoldMusic()
    {
        // Arrange
        var handler = new StubHttpMessageHandler(HttpStatusCode.OK, "{\"data\":{}}");
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        var orchestrator = CreateOrchestrator(handler, failureService.Object, null);
        var clientState = Encoding.UTF8.GetString(Convert.FromBase64String(new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = "caller-1",
        }.ToClientState()));

        // Act
        await orchestrator.AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = "agent-leg-1",
            ClientState = clientState,
        }, TestContext.Current.CancellationToken);

        // Assert
        Assert.EndsWith("calls/agent-leg-1/actions/bridge", handler.Requests[0].RequestUri.AbsolutePath, StringComparison.Ordinal);
        Assert.EndsWith("calls/caller-1/actions/playback_stop", handler.Requests[1].RequestUri.AbsolutePath, StringComparison.Ordinal);
    }

    private static TelnyxCallEvent PreDialedLegEvent(string eventType, string hangupCause = null)
        => new()
        {
            EventType = eventType,
            CallControlId = "leg-1",
            HangupCause = hangupCause,
            ClientState = Encoding.UTF8.GetString(Convert.FromBase64String(new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent,
                PeerCallControlId = "caller-1",
                ReservationId = "r1",
            }.ToClientState())),
        };

    private static TelnyxOptions CreateOptions()
        => new()
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

    private static TelnyxApiClient CreateApiClient(StubHttpMessageHandler handler)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new OptionsWrapper<TelnyxOptions>(CreateOptions()),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

    private static IOptionsMonitor<TelnyxOptions> CreateMonitor()
    {
        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(CreateOptions());

        return monitor.Object;
    }

    private static TelnyxContactCenterVoiceProvider CreateProvider(StubHttpMessageHandler handler, ITelnyxAgentEndpointResolver resolver)
    {
        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager
            .Setup(value => value.TryEnter(It.IsAny<string>()))
            .Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var localizer = new Mock<IStringLocalizer<TelnyxContactCenterVoiceProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));

        return new TelnyxContactCenterVoiceProvider(
            new Mock<ITelephonyProviderResolver>().Object,
            workManager.Object,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            resolver,
            CreateApiClient(handler),
            new Mock<IClock>().Object,
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            CreateMonitor(),
            localizer.Object);
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(
        StubHttpMessageHandler handler,
        IContactCenterAgentLegFailureService failureService,
        IAgentPreDialCoordinator coordinator)
        => new(
            CreateApiClient(handler),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            CreateMonitor(),
            failureService,
            [],
            coordinator is null ? [] : [coordinator]);
}
