using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Core.Services;
using CrestApps.OrchardCore.Telnyx;
using CrestApps.OrchardCore.Telnyx.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Telephony;

/// <summary>
/// What a caller pressed on an entry-point menu, as Telnyx reports it: a <c>call.gather.ended</c> webhook carrying
/// <c>digits</c> and a <c>status</c> of <c>valid</c>, <c>invalid</c>, <c>timeout</c>, <c>call_hangup</c>,
/// <c>cancelled</c> or <c>cancelled_amd</c>.
/// <para>
/// The event carries no call state, and it was mapped as a state before it was recognised as a key press: it was
/// dropped as unmappable, so no choice any caller made on a menu ever reached the menu.
/// </para>
/// </summary>
public sealed class TelnyxGatherEndedWebhookTests
{
    [Fact]
    public async Task AKeyPress_ReachesTheMenu()
    {
        // Arrange
        var harness = new WebhookHarness();

        // Act
        var result = await harness.DeliverAsync(Payload(digits: "2", status: "valid"));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Routed, result);
        var delivered = harness.Delivered.Single();
        Assert.Equal("v3:caller-1", delivered.ProviderCallId);
        Assert.Equal("2", delivered.Digits);
        Assert.Equal("gather-evt-1", delivered.DeliveryId);
        Assert.Equal(InboundVoiceDigitsOutcome.Collected, delivered.Outcome);
        Assert.Equal(TelnyxConstants.ProviderTechnicalName, delivered.ProviderName);
    }

    [Theory]
    [InlineData("valid", "1", InboundVoiceDigitsOutcome.Collected)]
    [InlineData("invalid", "9", InboundVoiceDigitsOutcome.Invalid)]
    [InlineData("timeout", "", InboundVoiceDigitsOutcome.TimedOut)]
    [InlineData("call_hangup", "", InboundVoiceDigitsOutcome.CallerHungUp)]
    [InlineData("cancelled", "", InboundVoiceDigitsOutcome.Cancelled)]
    [InlineData("cancelled_amd", "", InboundVoiceDigitsOutcome.Cancelled)]
    public async Task EachWayACollectionEnds_IsReportedToTheMenu(string status, string digits, InboundVoiceDigitsOutcome expected)
    {
        // Arrange
        var harness = new WebhookHarness();

        // Act
        await harness.DeliverAsync(Payload(digits, status));

        // Assert
        Assert.Equal(expected, harness.Delivered.Single().Outcome);
    }

    [Fact]
    public async Task AKeyPressNoMenuClaims_IsIgnored()
    {
        // Arrange
        var harness = new WebhookHarness { Claimed = false };

        // Act
        var result = await harness.DeliverAsync(Payload(digits: "1", status: "valid"));

        // Assert
        Assert.Equal(TelnyxWebhookResult.Ignored, result);
    }

    [Fact]
    public void TheStatus_IsReadOnlyFromTheGatherEvent()
    {
        // Arrange
        const string Answered = """
        { "data": { "id": "evt-2", "event_type": "call.answered", "payload": { "call_control_id": "v3:caller-1", "status": "valid" } } }
        """;

        // Act
        var parsedAnswered = TelnyxCallEventParser.TryParse(Answered, out var answered);
        var parsedGathered = TelnyxCallEventParser.TryParse(Payload("1", "valid"), out var gathered);

        // Assert
        Assert.True(parsedAnswered);
        Assert.True(parsedGathered);
        Assert.Null(answered.GatherStatus);
        Assert.Equal("valid", gathered.GatherStatus);
    }

    private static string Payload(string digits, string status)
        => $$"""
        {
          "data": {
            "record_type": "event",
            "event_type": "call.gather.ended",
            "id": "gather-evt-1",
            "occurred_at": "2026-09-25T15:00:00.000Z",
            "payload": {
              "call_control_id": "v3:caller-1",
              "call_leg_id": "leg-1",
              "call_session_id": "session-1",
              "from": "+17025550100",
              "to": "+17025550199",
              "digits": "{{digits}}",
              "status": "{{status}}"
            }
          }
        }
        """;

    private sealed class WebhookHarness
    {
        public WebhookHarness()
        {
            var sink = new Mock<IInboundVoiceDigitsSink>();
            sink.Setup(x => x.HandleDigitsAsync(It.IsAny<InboundVoiceDigitsEvent>(), It.IsAny<CancellationToken>()))
                .Callback<InboundVoiceDigitsEvent, CancellationToken>((digitsEvent, _) => Delivered.Add(digitsEvent))
                .ReturnsAsync(() => Claimed);

            var orchestrator = new Mock<ITelnyxOutboundBridgeOrchestrator>();
            orchestrator.Setup(x => x.AdvanceAsync(It.IsAny<TelnyxCallEvent>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(TelnyxOutboundBridgeLeg.None);

            // A key press is not a call-state transition: nothing about the call's state is ingested, and it is never
            // mistaken for a new inbound call.
            Service = new TelnyxWebhookService(
                new Mock<INormalizedVoiceEventIngestor>(MockBehavior.Strict).Object,
                new Mock<ITelnyxInboundCallRouter>(MockBehavior.Strict).Object,
                sink.Object,
                orchestrator.Object,
                [],
                [],
                new Mock<IClock>().Object,
                NullLogger<TelnyxWebhookService>.Instance);
        }

        public TelnyxWebhookService Service { get; }

        public bool Claimed { get; set; } = true;

        public List<InboundVoiceDigitsEvent> Delivered { get; } = [];

        public Task<TelnyxWebhookResult> DeliverAsync(string payload)
        {
            Assert.True(TelnyxCallEventParser.TryParse(payload, out var callEvent));

            return Service.ProcessAsync(callEvent, TestContext.Current.CancellationToken);
        }
    }
}
