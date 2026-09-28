using System.Net;
using System.Text;
using System.Text.Json;
using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
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
/// Warm transfer on Telnyx: the customer held in a consult conference with the queue's music, the agent's leg moved
/// in beside them, the destination rung on a leg of its own, and each phase undoing only what is still on the line.
/// </summary>
public sealed class TelnyxWarmTransferTests
{
    private const string CustomerLeg = "customer-leg";
    private const string AgentLeg = "agent-leg";
    private const string ConsultLeg = "consult-leg";

    [Fact]
    public async Task BeginConsult_HoldsTheCustomerWithMusic_MovesTheAgentIn_AndRingsTheColleague()
    {
        var handler = Telnyx();
        var resolver = new Mock<ITelnyxAgentEndpointResolver>();
        resolver.Setup(value => value.ResolveAsync("user-b", It.IsAny<CancellationToken>())).ReturnsAsync("sip:bea@sip.telnyx.com");

        var result = await CreateProvider(handler, resolver.Object).BeginConsultAsync(Request(new()
        {
            [ContactCenterConstants.AttendedTransferMetadata.AgentUserId] = "user-b",
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = "Agent",
            [ContactCenterConstants.AttendedTransferMetadata.HoldAudio] = "hold-clip",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(ConsultLeg, result.ProviderLegId);

        Assert.Equal(
            ["POST conferences", "POST conferences/conf-1/actions/hold", "POST conferences/conf-1/actions/join", "POST calls"],
            handler.Requests.Select(Describe).ToArray());

        using var conference = Body(handler, 0);
        Assert.Equal(CustomerLeg, conference.RootElement.GetProperty("call_control_id").GetString());
        Assert.Equal(TelnyxContactCenterVoiceProvider.ConsultConferenceName("consult-1"), conference.RootElement.GetProperty("name").GetString());

        using var hold = Body(handler, 1);
        Assert.Equal(CustomerLeg, hold.RootElement.GetProperty("call_control_ids")[0].GetString());
        Assert.Equal("hold-clip", hold.RootElement.GetProperty("media_name").GetString());

        using var join = Body(handler, 2);
        Assert.Equal(AgentLeg, join.RootElement.GetProperty("call_control_id").GetString());
        Assert.False(join.RootElement.TryGetProperty("end_conference_on_exit", out _));

        using var dial = Body(handler, 3);
        Assert.Equal("sip:bea@sip.telnyx.com", dial.RootElement.GetProperty("to").GetString());

        // A colleague's browser is not reached through the PSTN profile, or the leg never rings their credential.
        Assert.False(dial.RootElement.TryGetProperty("outbound_voice_profile_id", out _));

        var state = Encoding.UTF8.GetString(Convert.FromBase64String(dial.RootElement.GetProperty("client_state").GetString()));
        Assert.True(TelnyxOutboundBridgeState.TryParse(state, out var bridgeState));
        Assert.Equal(TelnyxOutboundBridgeState.ContactCenterConsultLegIntent, bridgeState.Intent);
        Assert.Equal(CustomerLeg, bridgeState.PeerCallControlId);
        Assert.Equal("consult-1", bridgeState.ConsultId);
    }

    [Fact]
    public async Task BeginConsult_ToAnOutsideNumber_DialsItThroughTheOutboundProfile()
    {
        var handler = Telnyx();

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>(), outboundVoiceProfileId: "ovp-1").BeginConsultAsync(Request(new()
        {
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = "External",
            ["targetAddress"] = "+15557654321",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);

        using var dial = Body(handler, 3);
        Assert.Equal("+15557654321", dial.RootElement.GetProperty("to").GetString());
        Assert.Equal("ovp-1", dial.RootElement.GetProperty("outbound_voice_profile_id").GetString());
    }

    [Fact]
    public async Task BeginConsult_WithoutTheAgentsLeg_IsRefusedBeforeAnythingIsSent()
    {
        var handler = Telnyx();
        var request = Request(new() { ["targetAddress"] = "+15557654321" });
        request.Metadata.Remove(ContactCenterConstants.AttendedTransferMetadata.AgentLegId);

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>()).BeginConsultAsync(request, TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task BeginConsult_WhenTheDestinationCannotBeDialled_GivesTheCustomerBackToTheAgent()
    {
        var handler = Telnyx(failDial: true);

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>()).BeginConsultAsync(Request(new()
        {
            ["targetAddress"] = "+15557654321",
        }), TestContext.Current.CancellationToken);

        Assert.False(result.Succeeded);
        Assert.Equal("POST conferences/conf-1/actions/unhold", Describe(handler.Requests[^1]));
    }

    [Fact]
    public async Task CompleteConsult_WithAColleague_UnholdsTheCustomer_AndLeavesTheAgentsLegForTheContactCenterToDrop()
    {
        var handler = Telnyx();

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>()).CompleteConsultAsync(Request(new()
        {
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = "Agent",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(
            ["GET conferences", "POST conferences/conf-1/actions/unhold"],
            handler.Requests.Select(Describe).ToArray());

        // Hanging the agent up here would race the Contact Center recording that their leg no longer carries the call.
        Assert.DoesNotContain(handler.Requests, request => request.RequestUri.AbsolutePath.EndsWith("/hangup", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CompleteConsult_WithAnOutsideParty_JoinsTheTwoPhoneLegsDirectly()
    {
        var handler = Telnyx();

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>()).CompleteConsultAsync(Request(new()
        {
            [ContactCenterConstants.AttendedTransferMetadata.TargetType] = "External",
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(
            [
                "GET conferences",
                "POST conferences/conf-1/actions/unhold",
                "POST conferences/conf-1/actions/leave",
                "POST conferences/conf-1/actions/leave",
                $"POST calls/{ConsultLeg}/actions/bridge",
            ],
            handler.Requests.Select(Describe).ToArray());

        using var bridge = Body(handler, 4);
        Assert.Equal(CustomerLeg, bridge.RootElement.GetProperty("call_control_id").GetString());
    }

    [Theory]
    [InlineData("agent", new[] { $"POST calls/{ConsultLeg}/actions/hangup", "GET conferences", "POST conferences/conf-1/actions/unhold" })]
    [InlineData("target", new[] { "GET conferences", "POST conferences/conf-1/actions/unhold" })]
    [InlineData("caller", new[] { $"POST calls/{ConsultLeg}/actions/hangup" })]
    public async Task CancelConsult_UndoesOnlyWhatIsStillOnTheLine(string endedBy, string[] expected)
    {
        var handler = Telnyx();

        var result = await CreateProvider(handler, Mock.Of<ITelnyxAgentEndpointResolver>()).CancelConsultAsync(Request(new()
        {
            [ContactCenterConstants.AttendedTransferMetadata.EndedBy] = endedBy,
        }), TestContext.Current.CancellationToken);

        Assert.True(result.Succeeded, result.ErrorMessage);
        Assert.Equal(expected, handler.Requests.Select(Describe).ToArray());
    }

    [Fact]
    public async Task ConsultLegAnswered_JoinsTheConference_AndTellsTheContactCenter()
    {
        var handler = Telnyx();
        var sink = new Mock<IConsultLegEventSink>();

        var leg = await CreateOrchestrator(handler, sink.Object).AdvanceAsync(ConsultLegEvent("call.answered"), TestContext.Current.CancellationToken);

        Assert.Equal(TelnyxOutboundBridgeLeg.DestinationLeg, leg);
        Assert.Equal(["GET conferences", "POST conferences/conf-1/actions/join"], handler.Requests.Select(Describe).ToArray());
        sink.Verify(value => value.OnAnsweredAsync(TelnyxConstants.ProviderTechnicalName, CustomerLeg, "consult-1", ConsultLeg, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsultLegHangup_AfterTheCallWasHandedToAnOutsideParty_ReleasesTheCustomer()
    {
        var handler = Telnyx();
        var sink = new Mock<IConsultLegEventSink>();
        sink.Setup(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, CustomerLeg, "consult-1", ConsultLeg, It.IsAny<HangupCause?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultLegEndedOutcome.ReleaseCustomer);

        await CreateOrchestrator(handler, sink.Object).AdvanceAsync(ConsultLegEvent("call.hangup"), TestContext.Current.CancellationToken);

        Assert.Equal([$"POST calls/{CustomerLeg}/actions/hangup"], handler.Requests.Select(Describe).ToArray());
    }

    [Fact]
    public async Task ConsultLegHangup_BeforeTheHandover_LeavesTheCustomerToTheContactCenter()
    {
        var handler = Telnyx();
        var sink = new Mock<IConsultLegEventSink>();
        sink.Setup(value => value.OnEndedAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<HangupCause?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ConsultLegEndedOutcome.ReturnedToAgent);

        await CreateOrchestrator(handler, sink.Object).AdvanceAsync(ConsultLegEvent("call.hangup"), TestContext.Current.CancellationToken);

        Assert.Empty(handler.Requests);
        sink.Verify(value => value.OnEndedAsync(TelnyxConstants.ProviderTechnicalName, CustomerLeg, "consult-1", ConsultLeg, It.IsAny<HangupCause?>(), It.IsAny<DateTime?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AgentLegAnswered_IsBridgedToParkAfterUnbridge_SoAWarmTransferCanMoveTheCallerOut()
    {
        var handler = Telnyx();
        var state = new TelnyxOutboundBridgeState
        {
            Intent = TelnyxOutboundBridgeState.ContactCenterAgentLegIntent,
            PeerCallControlId = CustomerLeg,
        };

        await CreateOrchestrator(handler, Mock.Of<IConsultLegEventSink>()).AdvanceAsync(new TelnyxCallEvent
        {
            EventType = "call.answered",
            CallControlId = AgentLeg,
            ClientState = state.ToClientStateJson(),
        }, TestContext.Current.CancellationToken);

        var bridgeIndex = handler.Requests.ToList().FindIndex(request => Describe(request) == $"POST calls/{AgentLeg}/actions/bridge");
        Assert.True(bridgeIndex >= 0);

        using var bridge = Body(handler, bridgeIndex);
        Assert.Equal("self", bridge.RootElement.GetProperty("park_after_unbridge").GetString());
    }

    private static TelnyxCallEvent ConsultLegEvent(string eventType)
        => new()
        {
            EventType = eventType,
            CallControlId = ConsultLeg,
            HangupCause = eventType == "call.hangup" ? "normal_clearing" : null,
            ClientState = new TelnyxOutboundBridgeState
            {
                Intent = TelnyxOutboundBridgeState.ContactCenterConsultLegIntent,
                PeerCallControlId = CustomerLeg,
                ConferenceName = TelnyxContactCenterVoiceProvider.ConsultConferenceName("consult-1"),
                ConsultId = "consult-1",
            }.ToClientStateJson(),
        };

    private static ContactCenterVoiceAttendedTransferRequest Request(Dictionary<string, string> metadata)
    {
        var request = new ContactCenterVoiceAttendedTransferRequest
        {
            InteractionId = "interaction-1",
            ProviderCallId = CustomerLeg,
            Metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["consultId"] = "consult-1",
                ["consultLegId"] = ConsultLeg,
                [ContactCenterConstants.AttendedTransferMetadata.AgentLegId] = AgentLeg,
            },
        };

        foreach (var entry in metadata)
        {
            request.Metadata[entry.Key] = entry.Value;
        }

        return request;
    }

    private static StubHttpMessageHandler Telnyx(bool failDial = false)
        => new(request =>
        {
            var path = request.RequestUri.AbsolutePath;
            var body = "{}";

            if (request.Method == HttpMethod.Post && path.EndsWith("/v2/conferences", StringComparison.Ordinal))
            {
                body = "{\"data\":{\"id\":\"conf-1\"}}";
            }
            else if (request.Method == HttpMethod.Get && path.EndsWith("/v2/conferences", StringComparison.Ordinal))
            {
                body = "{\"data\":[{\"id\":\"conf-1\"}]}";
            }
            else if (request.Method == HttpMethod.Post && path.EndsWith("/v2/calls", StringComparison.Ordinal))
            {
                if (failDial)
                {
                    return new HttpResponseMessage(HttpStatusCode.UnprocessableEntity) { Content = new StringContent("{\"errors\":[]}") };
                }

                body = $"{{\"data\":{{\"call_control_id\":\"{ConsultLeg}\"}}}}";
            }

            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body) };
        });

    private static string Describe(HttpRequestMessage request)
        => $"{request.Method.Method} {request.RequestUri.AbsolutePath["/v2/".Length..]}";

    private static JsonDocument Body(StubHttpMessageHandler handler, int index)
        => JsonDocument.Parse(handler.RequestBodies[index]);

    private static TelnyxOptions CreateOptions(string outboundVoiceProfileId = null)
        => new()
        {
            IsEnabled = true,
            ApiKey = "KEY",
            ConnectionId = "connection-1",
            ApiBaseUrl = "https://api.telnyx.test/v2/",
            DefaultOutboundCallerId = "+15550000000",
            OutboundVoiceProfileId = outboundVoiceProfileId,
        };

    private static TelnyxApiClient CreateApiClient(StubHttpMessageHandler handler, string outboundVoiceProfileId = null)
        => new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.telnyx.test/v2/") },
            new OptionsWrapper<TelnyxOptions>(CreateOptions(outboundVoiceProfileId)),
            new TelnyxApiRetryPolicy(TimeSpan.Zero),
            NullLogger<TelnyxApiClient>.Instance);

    private static IOptionsMonitor<TelnyxOptions> CreateMonitor(string outboundVoiceProfileId = null)
    {
        var monitor = new Mock<IOptionsMonitor<TelnyxOptions>>();
        monitor.SetupGet(value => value.CurrentValue).Returns(CreateOptions(outboundVoiceProfileId));

        return monitor.Object;
    }

    private static TelnyxContactCenterVoiceProvider CreateProvider(
        StubHttpMessageHandler handler,
        ITelnyxAgentEndpointResolver resolver,
        string outboundVoiceProfileId = null)
    {
        var workManager = new Mock<IContactCenterFeatureWorkManager>();
        workManager.Setup(value => value.TryEnter(It.IsAny<string>())).Returns(new Mock<IContactCenterFeatureWorkLease>().Object);

        var localizer = new Mock<IStringLocalizer<TelnyxContactCenterVoiceProvider>>();
        localizer.Setup(value => value[It.IsAny<string>()]).Returns<string>(name => new LocalizedString(name, name));

        return new TelnyxContactCenterVoiceProvider(
            new Mock<ITelephonyProviderResolver>().Object,
            workManager.Object,
            new Mock<ITelnyxAgentCredentialStore>().Object,
            resolver,
            CreateApiClient(handler, outboundVoiceProfileId),
            new Mock<IClock>().Object,
            NullLogger<TelnyxContactCenterVoiceProvider>.Instance,
            CreateMonitor(outboundVoiceProfileId),
            localizer.Object);
    }

    private static TelnyxOutboundBridgeOrchestrator CreateOrchestrator(StubHttpMessageHandler handler, IConsultLegEventSink sink)
        => new(
            CreateApiClient(handler),
            NullLogger<TelnyxOutboundBridgeOrchestrator>.Instance,
            CreateMonitor(),
            Mock.Of<IContactCenterAgentLegFailureService>(),
            [],
            [],
            [],
            [sink]);
}
