using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// A soft-phone dial to a disconnected number ended in silence. The agent's leg is answered before the number is
/// dialed and only bridged once the number answers, so the carrier's "not in service" announcement played on the
/// number's leg, which never reaches the agent, and the agent's leg was simply hung up. The agent is now told on their
/// own leg, which is hung up when the message ends.
/// </summary>
public sealed class TelnyxNotInServiceNoticeTests
{
    private const string AgentLegId = "agent-leg-1";
    private const string DestinationLegId = "dest-leg-1";
    private const string DialedNumber = "+14035550100";

    [Theory]
    // What Telnyx sent for a disconnected number on a live call: not_found with SIP 404.
    [InlineData("not_found", "404")]
    [InlineData("unallocated_number", null)]
    [InlineData(null, "410")]
    public async Task ANumberThatIsNotInService_IsAnnouncedOnTheAgentsLegInsteadOfHangingItUp(string hangupCause, string sipHangupCause)
    {
        // Arrange
        var handler = Provider(AgentLegState());
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(DestinationHangup(hangupCause, sipHangupCause), TestContext.Current.CancellationToken);

        // Assert
        var speak = Assert.Single(Posts(handler, $"calls/{AgentLegId}/actions/speak"));
        using var body = JsonDocument.Parse(speak);
        Assert.Equal(TelnyxOutboundBridgeOrchestrator.NotInServiceNotice, body.RootElement.GetProperty("payload").GetString());

        // The leg keeps everything its bridge state carried, so its own hang-up is still handled as the agent leg's.
        var state = ClientStateOf(body.RootElement);
        Assert.Equal(TelnyxOutboundBridgeState.AgentLegIntent, state.Intent);
        Assert.Equal(DialedNumber, state.Destination);
        Assert.Equal(DestinationLegId, state.PeerCallControlId);
        Assert.True(state.HangUpAfterNotice);

        Assert.Empty(Posts(handler, $"calls/{AgentLegId}/actions/hangup"));
    }

    [Theory]
    [InlineData("user_busy", "486")]
    [InlineData("normal_clearing", "200")]
    [InlineData("timeout", "480")]
    public async Task ANumberThatIsReachable_StillHangsUpTheAgentsLegWithNoMessage(string hangupCause, string sipHangupCause)
    {
        // Arrange
        var handler = Provider(AgentLegState());
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(DestinationHangup(hangupCause, sipHangupCause), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, $"calls/{AgentLegId}/actions/speak"));
        Assert.Single(Posts(handler, $"calls/{AgentLegId}/actions/hangup"));
    }

    [Fact]
    public async Task WhenTheMessageCannotBeStarted_TheAgentsLegIsHungUpAsBefore()
    {
        // Arrange
        var handler = Provider(AgentLegState(), refuseSpeak: true);
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(DestinationHangup("not_found", "404"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(Posts(handler, $"calls/{AgentLegId}/actions/speak"));
        Assert.Single(Posts(handler, $"calls/{AgentLegId}/actions/hangup"));
    }

    [Fact]
    public async Task WhenTheAgentsLegIsAlreadyGone_NothingIsSpoken()
    {
        // Arrange
        var handler = Provider(AgentLegState(), agentLegAlive: false);
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(DestinationHangup("not_found", "404"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, $"calls/{AgentLegId}/actions/speak"));
    }

    [Fact]
    public async Task TheEndOfTheMessage_HangsUpTheAgentsLeg()
    {
        // Arrange
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(AgentLegSpeakEnded(AgentLegState().AsHangUpAfterNotice()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Single(Posts(handler, $"calls/{AgentLegId}/actions/hangup"));
    }

    [Fact]
    public async Task TheEndOfAnyOtherMessageOnTheAgentsLeg_LeavesTheCallUp()
    {
        // Arrange
        var handler = Provider(agentLegState: null);
        var orchestrator = CreateOrchestrator(handler);

        // Act
        await orchestrator.AdvanceAsync(AgentLegSpeakEnded(AgentLegState()), TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(Posts(handler, $"calls/{AgentLegId}/actions/hangup"));
    }

    private static TelnyxOutboundBridgeState AgentLegState()
        => new()
        {
            Intent = TelnyxOutboundBridgeState.AgentLegIntent,
            Destination = DialedNumber,
            CallerId = "+15550000000",
            PeerCallControlId = DestinationLegId,
        };

    private static TelnyxCallEvent DestinationHangup(string hangupCause, string sipHangupCause)
        => new()
        {
            EventType = "call.hangup",
            CallControlId = DestinationLegId,
            HangupCause = hangupCause,
            SipHangupCause = sipHangupCause,
            To = DialedNumber,
            ClientState = Decode(new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.DestinationLegIntent,
                PeerCallControlId = AgentLegId,
            }),
        };

    private static TelnyxCallEvent AgentLegSpeakEnded(TelnyxOutboundBridgeState state)
        => new()
        {
            EventType = "call.speak.ended",
            CallControlId = AgentLegId,
            ClientState = Decode(state),
        };

    // Answers every call-control request. A leg read back reports whether it is alive and the state it carries; a speak
    // command can be refused.
    private static StubHttpMessageHandler Provider(TelnyxOutboundBridgeState agentLegState, bool agentLegAlive = true, bool refuseSpeak = false)
        => new(request =>
        {
            if (refuseSpeak &&
                request.Method == HttpMethod.Post &&
                request.RequestUri.AbsolutePath.EndsWith("/actions/speak", StringComparison.Ordinal))
            {
                return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity)
                {
                    Content = new StringContent("{\"errors\":[{\"code\":\"90018\"}]}"),
                };
            }

            var data = new Dictionary<string, object>
            {
                ["call_control_id"] = "new-leg",
                ["is_alive"] = agentLegAlive,
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

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(StubHttpMessageHandler handler)
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
            new TestOptionsMonitor<TelnyxOptions>(options),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

        return new TelnyxOutboundBridgeOrchestrator(
            apiClient,
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            monitor.Object,
            new Mock<IContactCenterAgentLegFailureService>().Object,
            [],
            [],
            []);
    }
}
