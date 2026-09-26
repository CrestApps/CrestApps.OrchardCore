using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telephony;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A leg rung to a user's browser right after their phone reopened went to the credential the phone had left behind:
/// the store still had it as registered, and the phone was registering on a fresh one. Telnyx refused it with SIP 480
/// and the call ended while the phone sat ready. A leg refused as unavailable (480 or 404) is now rung once more on the
/// user's current credential; a busy or declined one (486, 603) is not, and neither is a leg that was already a retry.
/// </summary>
public sealed class TelnyxAgentLegRedeliveryTests
{
    private const string StaleEndpoint = "sip:gencredOld@sip.telnyx.com";
    private const string CurrentEndpoint = "sip:gencredNew@sip.telnyx.com";

    [Fact]
    public async Task AnExtensionCallsDestinationLeg_RefusedAsUnavailable_IsRungOnceOnTheTargetsCurrentCredential()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        resolver
            .Setup(value => value.ResolveRedeliveryAsync("target-user", StaleEndpoint, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxAgentEndpointRedelivery(CurrentEndpoint, "credential-new", "credential-old"));
        var handler = Provider(agentLegState: ExtensionAgentLegState());
        var orchestrator = CreateOrchestrator(handler, resolver.Object);

        // Act
        await orchestrator.AdvanceAsync(ExtensionDestinationHangup("480"), TestContext.Current.CancellationToken);

        // Assert
        var dial = Assert.Single(Posts(handler, "calls"));
        using var body = JsonDocument.Parse(dial);
        Assert.Equal(CurrentEndpoint, body.RootElement.GetProperty("to").GetString());
        Assert.Equal("Mike", body.RootElement.GetProperty("from_display_name").GetString());
        Assert.NotEqual("ob-dest-agent-leg-1", body.RootElement.GetProperty("command_id").GetString());

        var state = ClientStateOf(body.RootElement);
        Assert.Equal(TelnyxOutboundBridgeState.DestinationLegIntent, state.Intent);
        Assert.Equal("agent-leg-1", state.PeerCallControlId);
        Assert.Equal("target-user", state.VoicemailRecipientUserId);
        Assert.True(state.Redelivered);

        // The caller's leg now names the new leg, and is neither hung up nor sent to voicemail.
        var update = Assert.Single(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/client_state_update", StringComparison.Ordinal));
        Assert.NotNull(update);
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/hangup", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/record_start", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("486", "USER_BUSY")]
    [InlineData("603", "CALL_REJECTED")]
    public async Task AnExtensionCallsDestinationLeg_ThatWasBusyOrDeclined_IsNotRungAgain(string sipCause, string hangupCause)
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        var handler = Provider(agentLegState: ExtensionAgentLegState());
        var orchestrator = CreateOrchestrator(handler, resolver.Object);

        // Act
        await orchestrator.AdvanceAsync(ExtensionDestinationHangup(sipCause, hangupCause), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, "calls"));
        resolver.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task AnExtensionCallsDestinationLeg_ThatWasAlreadyARetry_IsNotRungAgain_AndTheCallerReachesVoicemail()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        var handler = Provider(agentLegState: ExtensionAgentLegState());
        var orchestrator = CreateOrchestrator(handler, resolver.Object);

        // Act
        await orchestrator.AdvanceAsync(ExtensionDestinationHangup("480", redelivered: true), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, "calls"));
        resolver.VerifyNoOtherCalls();
        Assert.Contains(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/record_start", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnExtensionCallsDestinationLeg_RefusedAsUnavailable_WithNoOtherCredential_SendsTheCallerToVoicemail()
    {
        // Arrange
        // The target's phone is simply not there: that is an unavailable extension, which goes to voicemail like one
        // that rang out, rather than hanging up on the caller.
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveRedeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((TelnyxAgentEndpointRedelivery)null);
        var handler = Provider(agentLegState: ExtensionAgentLegState());
        var orchestrator = CreateOrchestrator(handler, resolver.Object);

        // Act
        await orchestrator.AdvanceAsync(ExtensionDestinationHangup("480"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, "calls"));
        Assert.Contains(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/record_start", StringComparison.Ordinal));
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("calls/agent-leg-1/actions/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AContactCenterAgentLeg_RefusedAsUnavailable_IsRungOnceOnTheAgentsCurrentCredential_AndTheCallIsNotFailed()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        resolver
            .Setup(value => value.ResolveRedeliveryAsync("agent-user", StaleEndpoint, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxAgentEndpointRedelivery(CurrentEndpoint, "credential-new", "credential-old"));
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler, resolver.Object, failureService.Object);

        // Act
        await orchestrator.AdvanceAsync(
            AgentLegHangup(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, "480"),
            TestContext.Current.CancellationToken);

        // Assert
        var dial = Assert.Single(Posts(handler, "calls"));
        using var body = JsonDocument.Parse(dial);
        Assert.Equal(CurrentEndpoint, body.RootElement.GetProperty("to").GetString());

        var state = ClientStateOf(body.RootElement);
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, state.Intent);
        Assert.Equal("caller-1", state.PeerCallControlId);
        Assert.Equal("agent-user", state.RingUserId);
        Assert.True(state.Redelivered);

        failureService.Verify(
            service => service.FailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task AContactCenterAgentLeg_ThatWasBusy_FailsTheCallWithoutRingingAgain()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler, resolver.Object, failureService.Object);

        // Act
        await orchestrator.AdvanceAsync(
            AgentLegHangup(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, "486", "USER_BUSY"),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, "calls"));
        failureService.Verify(service => service.FailAsync("Telnyx", "caller-1", HangupCause.Busy, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AContactCenterAgentLeg_ThatWasAlreadyARetry_FailsTheCallWithoutRingingAgain()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        var failureService = new Mock<IContactCenterAgentLegFailureService>();
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler, resolver.Object, failureService.Object);

        // Act
        await orchestrator.AdvanceAsync(
            AgentLegHangup(TelnyxOutboundBridgeState.ContactCenterAgentLegIntent, "480", redelivered: true),
            TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, "calls"));
        failureService.Verify(service => service.FailAsync("Telnyx", "caller-1", HangupCause.NoAnswer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task APreDialedAgentLeg_RefusedAsUnavailable_IsRungAgainThroughTheCoordinator()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>(MockBehavior.Strict);
        resolver
            .Setup(value => value.ResolveRedeliveryAsync(
                "agent-user",
                StaleEndpoint,
                TelephonyConstants.SoftPhoneClientCapabilities.HeldOfferLeg,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxAgentEndpointRedelivery(CurrentEndpoint, "credential-new", "credential-old"));
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        coordinator
            .Setup(value => value.RedialAgentLegAsync("Telnyx", "r1", "leg-1", CurrentEndpoint, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler, resolver.Object, coordinator: coordinator.Object);

        // Act
        await orchestrator.AdvanceAsync(
            AgentLegHangup(TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent, "480"),
            TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(value => value.RedialAgentLegAsync("Telnyx", "r1", "leg-1", CurrentEndpoint, It.IsAny<CancellationToken>()), Times.Once);
        coordinator.Verify(
            value => value.OnAgentLegEndedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task APreDialedAgentLeg_ThatCouldNotBeRungAgain_EndsTheWayItAlwaysDid()
    {
        // Arrange
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver
            .Setup(value => value.ResolveRedeliveryAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelnyxAgentEndpointRedelivery(CurrentEndpoint, "credential-new", "credential-old"));
        var coordinator = new Mock<IAgentPreDialCoordinator>();
        coordinator
            .Setup(value => value.RedialAgentLegAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler, resolver.Object, coordinator: coordinator.Object);

        // Act
        await orchestrator.AdvanceAsync(
            AgentLegHangup(TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent, "480"),
            TestContext.Current.CancellationToken);

        // Assert
        coordinator.Verify(
            value => value.OnAgentLegEndedAsync("Telnyx", "r1", "leg-1", HangupCause.NoAnswer, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private static TelnyxOutboundBridgeState ExtensionAgentLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = StaleEndpoint,
            CallerId = "+15550000000",
            CallerDisplayName = "Mike",
            VoicemailRecipientUserId = "target-user",
            RingTimeoutSeconds = 25,
            PeerCallControlId = "dest-leg-1",
        };

    private static TelnyxCallEvent ExtensionDestinationHangup(string sipCause, string hangupCause = "NORMAL_CLEARING", bool redelivered = false)
        => new()
        {
            EventType = "call.hangup",
            CallControlId = "dest-leg-1",
            HangupCause = hangupCause,
            SipHangupCause = sipCause,
            To = StaleEndpoint,
            ClientState = Decode(new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = "agent-leg-1",
                VoicemailRecipientUserId = "target-user",
                Redelivered = redelivered ? true : null,
            }),
        };

    private static TelnyxCallEvent AgentLegHangup(string intent, string sipCause, string hangupCause = "NORMAL_CLEARING", bool redelivered = false)
        => new()
        {
            EventType = "call.hangup",
            CallControlId = "leg-1",
            HangupCause = hangupCause,
            SipHangupCause = sipCause,
            To = StaleEndpoint,
            ClientState = Decode(new TelnyxOutboundBridgeState
            {
                Intent = intent,
                PeerCallControlId = "caller-1",
                ReservationId = intent == TelnyxOutboundBridgeState.ContactCenterPreDialedAgentLegIntent ? "r1" : null,
                RingUserId = "agent-user",
                Redelivered = redelivered ? true : null,
            }),
        };

    // Answers every call-control request: a new leg's id, and for a leg read back, that it is alive and what it carries.
    private static StubHttpMessageHandler Provider(TelnyxOutboundBridgeState agentLegState)
        => new(request =>
        {
            var data = new Dictionary<string, object>
            {
                ["call_control_id"] = "new-leg",
                ["is_alive"] = true,
            };

            if (agentLegState is not null && request.Method == HttpMethod.Get)
            {
                data["client_state"] = agentLegState.ToClientState();
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { data })),
            };
        });

    private static List<string> Posts(StubHttpMessageHandler handler, string path)
        => handler.Requests
            .Select((request, index) => (request, body: handler.RequestBodies[index]))
            .Where(entry => entry.request.Method == HttpMethod.Post && entry.request.RequestUri.AbsolutePath.EndsWith("/v2/" + path, StringComparison.Ordinal))
            .Select(entry => entry.body)
            .ToList();

    private static TelnyxOutboundBridgeState ClientStateOf(JsonElement body)
    {
        var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(body.GetProperty("client_state").GetString()));
        Assert.True(TelnyxOutboundBridgeState.TryParse(decoded, out var state));

        return state;
    }

    private static string Decode(TelnyxOutboundBridgeState state)
        => Encoding.UTF8.GetString(Convert.FromBase64String(state.ToClientState()));

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(
        StubHttpMessageHandler handler,
        ITelnyxAgentEndpointResolver resolver,
        IContactCenterAgentLegFailureService failureService = null,
        IAgentPreDialCoordinator coordinator = null)
    {
        var options = new TelnyxOptions
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
        };

        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(options);

        var apiClient = new TelnyxApiClient(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new OptionsWrapper<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxOutboundBridgeOrchestrator(
            apiClient,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            monitor.Object,
            failureService ?? new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            coordinator is null ? [] : [coordinator],
            [],
            agentEndpointResolver: resolver);
    }
}
