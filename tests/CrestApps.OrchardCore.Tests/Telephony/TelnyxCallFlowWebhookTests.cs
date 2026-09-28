using System.Net;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Telephony.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// Webhooks the platform marked a command to hear back about: the leg an external transfer rang, whose answer or
/// hang-up says whether the caller was put through, and the end of a last message after which the call is hung up.
/// <para>
/// Telnyx documents that a transfer which fails sends <c>call.hangup</c> for the leg it rang and leaves the caller's
/// leg up for further commands. That hang-up belonged to no call the platform tracked and was dropped, so a caller whose
/// chosen number was busy was left on a silent line.
/// </para>
/// </summary>
public sealed class TelnyxCallFlowWebhookTests
{
    [Theory]
    [InlineData("user_busy")]
    [InlineData("timeout")]
    [InlineData("call_rejected")]
    public async Task ATransferLegThatHangsUpUnanswered_IsReportedAsAFailedTransfer(string cause)
    {
        // Arrange
        var harness = new WebhookHarness();

        // Act
        var result = await harness.DeliverAsync(Event("call.hangup", "v3:partner-leg", TransferLegState(), cause));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Routed, result);
        var outcome = harness.Outcomes.Single();
        Assert.Equal("interaction-1", outcome.InteractionId);
        Assert.Equal("v3:partner-leg", outcome.ProviderLegId);
        Assert.False(outcome.Answered);
        Assert.False(outcome.CallerLeft);
        Assert.Equal(cause, outcome.HangupCause);
    }

    [Theory]
    [InlineData("call.answered")]
    [InlineData("call.bridged")]
    public async Task ATransferLegThatAnswers_IsReportedAsConnected(string eventType)
    {
        // Arrange
        var harness = new WebhookHarness();

        // Act
        await harness.DeliverAsync(Event(eventType, "v3:partner-leg", TransferLegState()));

        // Assert
        Assert.True(harness.Outcomes.Single().Answered);
    }

    [Fact]
    public async Task ATransferLegCancelledBecauseTheCallerHungUp_IsReportedAsTheCallerLeaving()
    {
        // Arrange
        // Nobody is left to put anywhere else.
        var harness = new WebhookHarness();

        // Act
        await harness.DeliverAsync(Event("call.hangup", "v3:partner-leg", TransferLegState(), "originator_cancel"));

        // Assert
        Assert.True(harness.Outcomes.Single().CallerLeft);
    }

    [Fact]
    public async Task ATransferLegsOtherEvents_AreNeitherReportedNorTreatedAsACall()
    {
        // Arrange
        // The strict ingestor fails the test if the leg is ever mistaken for a call the platform tracks.
        var harness = new WebhookHarness();

        // Act
        var result = await harness.DeliverAsync(Event("call.initiated", "v3:partner-leg", TransferLegState()));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Ignored, result);
        Assert.Empty(harness.Outcomes);
    }

    [Fact]
    public async Task TheEndOfALastMessage_HangsTheCallUp()
    {
        // Arrange
        // A caller who took a callback is told it is arranged; the call ends when that has been said.
        var harness = new WebhookHarness();

        // Act
        var result = await harness.DeliverAsync(Event("call.speak.ended", "v3:caller-1", HangUpState()));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Updated, result);
        Assert.Equal("/v2/calls/v3:caller-1/actions/hangup", Uri.UnescapeDataString(harness.Http.Requests.Single().Path));
    }

    [Fact]
    public async Task TheCallersOwnHangUp_CarryingTheSameState_GoesOnToTheOrdinaryPipeline()
    {
        // Arrange
        // Telnyx repeats a command's client_state on every later webhook for the leg, so the caller's hang-up arrives
        // with it too. It is still the end of the call and has to be recorded as one.
        var harness = new WebhookHarness();

        // Act
        await harness.DeliverAsync(Event("call.hangup", "v3:caller-1", HangUpState(), "normal_clearing"));

        // Assert
        Assert.Empty(harness.Http.Requests);
        Assert.Equal(VoiceCallState.Ended, harness.Ingested.Single().State);
    }

    [Fact]
    public async Task TheEndOfAnEntryPointMessage_IsHandedToTheDigitsSinksAnnouncementHandler()
    {
        // Arrange
        // Without this the caller who heard the welcome or closed message was never moved on to the menu, the queue or
        // voicemail: the speak.ended was dropped as an unmappable call event.
        var harness = new WebhookHarness { AnnouncementClaims = true };

        // Act
        var result = await harness.DeliverAsync(Event("call.speak.ended", "v3:caller-1", AnnouncementState()));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Routed, result);
        var ended = harness.AnnouncementsEnded.Single();
        Assert.Equal(TelnyxConstants.ProviderTechnicalName, ended.ProviderName);
        Assert.Equal("v3:caller-1", ended.ProviderCallId);
        Assert.Equal("evt-call.speak.ended", ended.DeliveryId);

        // It is not the hang-up state: the call carries on.
        Assert.Empty(harness.Http.Requests);
        Assert.Empty(harness.Ingested);
    }

    [Fact]
    public async Task TheEndOfSpeechTheSinkDoesNotClaim_IsNotHungUp()
    {
        // Arrange
        // The announcement state stays on the leg, so a queue announcement's speak.ended carries it too. The sink says
        // the caller is not waiting on a message, and the call must not be touched.
        var harness = new WebhookHarness { AnnouncementClaims = false };

        // Act
        var result = await harness.DeliverAsync(Event("call.speak.ended", "v3:caller-1", AnnouncementState()));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Ignored, result);
        Assert.Single(harness.AnnouncementsEnded);
        Assert.Empty(harness.Http.Requests);
    }

    [Theory]
    [InlineData("call.hangup", VoiceCallState.Ended)]
    [InlineData("call.answered", VoiceCallState.Connected)]
    public async Task TheCallersOtherEvents_CarryingTheAnnouncementState_GoOnToTheOrdinaryPipeline(string eventType, VoiceCallState state)
    {
        // Arrange
        // A hang-up taken for the end of the message would move a caller who has gone, and lose the end of the call.
        var harness = new WebhookHarness { AnnouncementClaims = true };

        // Act
        await harness.DeliverAsync(Event(eventType, "v3:caller-1", AnnouncementState(), eventType == "call.hangup" ? "normal_clearing" : null));

        // Assert
        Assert.Empty(harness.AnnouncementsEnded);
        Assert.Empty(harness.Http.Requests);
        Assert.Equal(state, harness.Ingested.Single().State);
    }

    [Fact]
    public async Task TheEndOfAClosedMessageBeforeReject_HangsUpRatherThanMovingTheCaller()
    {
        // Arrange
        var harness = new WebhookHarness { AnnouncementClaims = true };

        // Act
        await harness.DeliverAsync(Event("call.speak.ended", "v3:caller-1", HangUpState()));

        // Assert
        Assert.Empty(harness.AnnouncementsEnded);
        Assert.Equal("/v2/calls/v3:caller-1/actions/hangup", Uri.UnescapeDataString(harness.Http.Requests.Single().Path));
    }

    private static string AnnouncementState()
        => TelnyxCallFlowClientState.ForAnnouncement().ToClientState();

    private static string TransferLegState()
        => TelnyxCallFlowClientState.ForTransferLeg("interaction-1").ToClientState();

    private static string HangUpState()
        => TelnyxCallFlowClientState.ForHangUpAfterSpeech().ToClientState();

    private static string Event(string eventType, string callControlId, string clientState, string hangupCause = null)
        => $$"""
        {
          "data": {
            "record_type": "event",
            "event_type": "{{eventType}}",
            "id": "evt-{{eventType}}",
            "occurred_at": "2026-09-25T15:00:00.000Z",
            "payload": {
              "call_control_id": "{{callControlId}}",
              "call_leg_id": "leg-1",
              "call_session_id": "session-1",
              "direction": "outgoing",
              "client_state": "{{clientState}}"{{(hangupCause is null ? string.Empty : $", \"hangup_cause\": \"{hangupCause}\"")}}
            }
          }
        }
        """;

    private sealed class WebhookHarness
    {
        public WebhookHarness()
        {
            var sink = new Mock<IExternalTransferOutcomeSink>();
            sink.Setup(x => x.HandleAsync(It.IsAny<ExternalTransferOutcome>(), It.IsAny<CancellationToken>()))
                .Callback<ExternalTransferOutcome, CancellationToken>((outcome, _) => Outcomes.Add(outcome))
                .ReturnsAsync(true);

            var orchestrator = new Mock<ITelnyxOutboundBridgeOrchestrator>();
            orchestrator.Setup(x => x.AdvanceAsync(It.IsAny<TelnyxCallEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TelnyxOutboundBridgeLeg.None);

            var ingestor = new Mock<INormalizedVoiceEventIngestor>(MockBehavior.Strict);
            ingestor.Setup(x => x.IngestAsync(It.Is<ProviderVoiceEvent>(value => value.ProviderCallId == "v3:caller-1"), It.IsAny<CancellationToken>()))
                .Callback<ProviderVoiceEvent, CancellationToken>((voiceEvent, _) => Ingested.Add(voiceEvent))
                .ReturnsAsync(true);

            var apiClient = new TelnyxApiClient(
                new HttpClient(Http) { BaseAddress = new Uri("https://api.telnyx.com/v2/") },
                new TestOptionsMonitor<TelnyxOptions>(new TelnyxOptions { ApiBaseUrl = "https://api.telnyx.com/v2/", ApiKey = "KEY" }),
                new TelnyxApiRetryPolicy(TimeSpan.Zero),
                NullLogger<TelnyxApiClient>.Instance);

            // Strict, so a key press or anything other than the end of an entry point message fails the test.
            var digitsSink = new Mock<IInboundVoiceDigitsSink>(MockBehavior.Strict);
            digitsSink.Setup(x => x.HandleAnnouncementEndedAsync(It.IsAny<InboundVoiceAnnouncementEndedEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InboundVoiceAnnouncementEndedEvent, CancellationToken>((announcementEvent, _) => AnnouncementsEnded.Add(announcementEvent))
                .ReturnsAsync(() => AnnouncementClaims);

            Service = new TelnyxWebhookService(
                ingestor.Object,
                new Mock<ITelnyxInboundCallRouter>(MockBehavior.Strict).Object,
                digitsSink.Object,
                orchestrator.Object,
                [],
                [],
                sink.Object,
                apiClient,
                new Mock<IClock>().Object,
                NullLogger<TelnyxWebhookService>.Instance);
        }

        public RecordingHttpMessageHandler Http { get; } = new RecordingHttpMessageHandler().AlwaysRespondWith(HttpStatusCode.OK);

        public TelnyxWebhookService Service { get; }

        public List<ExternalTransferOutcome> Outcomes { get; } = [];

        public List<ProviderVoiceEvent> Ingested { get; } = [];

        public List<InboundVoiceAnnouncementEndedEvent> AnnouncementsEnded { get; } = [];

        public bool AnnouncementClaims { get; set; }

        public Task<TelnyxWebhookResult> DeliverAsync(string payload)
        {
            Assert.True(TelnyxCallEventParser.TryParse(payload, out var callEvent));
            Assert.False(string.IsNullOrEmpty(callEvent.ClientState));

            return Service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);
        }
    }
}
