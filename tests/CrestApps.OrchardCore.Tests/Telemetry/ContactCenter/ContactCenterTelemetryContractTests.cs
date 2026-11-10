using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Telemetry;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Diagnostics;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Telemetry.ContactCenter;

/// <summary>
/// The Contact Center funnels emit the instruments and spans the production support docs list, with bounded tags only.
/// Their names are a public contract that dashboards and alerts depend on.
/// </summary>
public sealed class ContactCenterTelemetryContractTests
{
    private static readonly DateTime _now = new(2026, 11, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void CallFunnel_RecordsTheProviderEventItsLagAndWhatTheCallCameTo()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var session = new CallSession
        {
            ItemId = "session-1",
            ProviderName = "Telnyx",
            Direction = InteractionDirection.Inbound,
            CreatedUtc = _now,
            StartedUtc = _now,
            AnsweredUtc = _now.AddSeconds(4),
            EndedUtc = _now.AddSeconds(64),
            HangupCause = HangupCause.NormalClearing,
        };
        var interaction = new Interaction { ItemId = "interaction-1", QueueId = "queue-1", AgentId = "agent-1" };

        // Act
        using (var activity = ContactCenterDiagnostics.StartCallIngest("Telnyx", VoiceCallState.Ended))
        {
            ContactCenterDiagnostics.RecordProviderEvent(activity, "Telnyx", VoiceCallState.Ended, ContactCenterDiagnostics.ProviderEventApplied, TimeSpan.FromSeconds(2));
            ContactCenterDiagnostics.RecordCallEnded(session, interaction, "caller");
        }

        // Assert
        var providerEvent = Assert.Single(capture.Find("crestapps.contactcenter.provider_event"));
        Assert.Equal("Telnyx", providerEvent.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]);
        Assert.Equal(nameof(VoiceCallState.Ended), providerEvent.Tags["state"]);
        Assert.Equal("applied", providerEvent.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);

        Assert.Equal(2, Assert.Single(capture.Find("crestapps.contactcenter.provider_event.lag")).Value);
        Assert.Equal(4, Assert.Single(capture.Find("crestapps.contactcenter.call.setup.duration")).Value);
        Assert.Equal(64, Assert.Single(capture.Find("crestapps.contactcenter.call.duration")).Value);

        var completed = Assert.Single(capture.Find("crestapps.contactcenter.call.completed"));
        Assert.Equal("inbound", completed.Tags[CrestAppsOrchardCoreTelemetry.DirectionTag]);
        Assert.Equal(nameof(HangupCause.NormalClearing), completed.Tags["hangup.cause"]);
        Assert.Equal("caller", completed.Tags["hangup.source"]);

        // An answered call was not abandoned.
        Assert.Empty(capture.Find("crestapps.contactcenter.call.abandoned"));

        var span = Assert.Single(capture.Spans, activity => activity.OperationName == "crestapps.contactcenter.call.ingest");
        Assert.Equal("applied", span.GetTagItem(CrestAppsOrchardCoreTelemetry.ResultTag));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void CallFunnel_AnInboundCallerWhoHungUpWaiting_IsAnAbandonInTheQueueStage()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var session = new CallSession
        {
            Direction = InteractionDirection.Inbound,
            CreatedUtc = _now,
            StartedUtc = _now,
            EndedUtc = _now.AddSeconds(30),
            HangupCause = HangupCause.NormalClearing,
        };
        var interaction = new Interaction { QueueId = "queue-1" };

        // Act
        ContactCenterDiagnostics.RecordCallEnded(session, interaction, hangupSource: null);

        // Assert
        var abandoned = Assert.Single(capture.Find("crestapps.contactcenter.call.abandoned"));
        Assert.Equal("queue", abandoned.Tags["stage"]);
        Assert.Equal("queue", abandoned.Tags["kind"]);
        Assert.Empty(capture.Find("crestapps.contactcenter.call.setup.duration"));
    }

    [Fact]
    public void Offers_RecordTheSettlementItsRingTimeAndWhyAReservationWasNotMade()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var reservation = new ActivityReservation { ItemId = "reservation-1", QueueId = ContactCenterConstants.DirectRouting.QueueId, CreatedUtc = _now };

        // Act
        using (var activity = ContactCenterDiagnostics.StartOffer("accept", reservation.QueueId))
        {
            ContactCenterDiagnostics.RecordOfferSettled(activity, ContactCenterDiagnostics.OfferAccepted, reservation, InteractionChannel.Voice, _now.AddSeconds(7));
        }

        ContactCenterDiagnostics.RecordReserve(activity: null, ContactCenterDiagnostics.ReserveLockTimeout, "queue-1");
        ContactCenterDiagnostics.RecordCasConflict("reservation.accept");

        // Assert
        var settled = Assert.Single(capture.Find("crestapps.contactcenter.offer.settled"));
        Assert.Equal("accepted", settled.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("voice", settled.Tags[CrestAppsOrchardCoreTelemetry.ChannelTag]);
        Assert.Equal("direct", settled.Tags["kind"]);

        Assert.Equal(7, Assert.Single(capture.Find("crestapps.contactcenter.offer.ring.duration")).Value);

        var reserve = Assert.Single(capture.Find("crestapps.contactcenter.reservation.reserve"));
        Assert.Equal("lock_timeout", reserve.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal("queue", reserve.Tags["kind"]);

        Assert.Equal("reservation.accept", Assert.Single(capture.Find("crestapps.contactcenter.cas.conflict")).Tags["site"]);
        Assert.Contains(capture.Spans, activity => activity.OperationName == "crestapps.contactcenter.offer");
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void Queues_RecordTheWaitByHowTheCallerLeft_AndTheOuterExitScopeWins()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();
        var queueItem = new QueueItem { QueueId = "queue-1", EnqueuedUtc = _now, QueueEnteredUtc = _now };

        // Act
        ContactCenterDiagnostics.RecordQueueEntered(queueItem, InteractionChannel.Voice);

        // The maximum-wait action sends the caller to voicemail; the voicemail path names its own exit, but the caller
        // timed out.
        using (ContactCenterDiagnostics.BeginQueueExit(ContactCenterDiagnostics.QueueExitTimeout))
        using (ContactCenterDiagnostics.BeginQueueExit(ContactCenterDiagnostics.QueueExitVoicemail))
        {
            ContactCenterDiagnostics.RecordQueueExit(queueItem, ContactCenterDiagnostics.QueueExitRemoved, InteractionChannel.Voice, _now.AddSeconds(90));
        }

        ContactCenterDiagnostics.RecordQueueExit(queueItem, ContactCenterDiagnostics.QueueExitAssigned, InteractionChannel.Voice, _now.AddSeconds(20));

        // Assert
        Assert.Equal("voice", Assert.Single(capture.Find("crestapps.contactcenter.queue.item")).Tags[CrestAppsOrchardCoreTelemetry.ChannelTag]);

        var waits = capture.Find("crestapps.contactcenter.queue.wait.duration");
        Assert.Equal(2, waits.Count);
        Assert.Equal(90, waits.Single(wait => Equals(wait.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], "timeout")).Value);
        Assert.Equal(20, waits.Single(wait => Equals(wait.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag], "assigned")).Value);
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void Outbox_DeadLetter_IsCountedOnTheDeprecatedAndTheNewInstrument()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();

        // Act
        ContactCenterDiagnostics.RecordOutboxDeadLettered("retry_exhausted");
        ContactCenterDiagnostics.RecordOutboxHandler("ContactCenter/RealTimeProjection/v1", TimeSpan.FromMilliseconds(15), new InvalidOperationException());

        // Assert
        Assert.Equal("retry_exhausted", Assert.Single(capture.Find("contactcenter.outbox.dead_lettered")).Tags[CrestAppsOrchardCoreTelemetry.ReasonTag]);
        Assert.Equal("retry_exhausted", Assert.Single(capture.Find("crestapps.contactcenter.outbox.dead_lettered")).Tags[CrestAppsOrchardCoreTelemetry.ReasonTag]);

        var handler = Assert.Single(capture.Find("crestapps.contactcenter.outbox.handler.duration"));
        Assert.Equal("ContactCenter/RealTimeProjection/v1", handler.Tags["handler"]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeError, handler.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
    }

    [Fact]
    public void InboundRoute_RecordsTheSpanAndTheRoutedCallWithABoundedReason()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();

        // Act
        using (var telemetry = ContactCenterDiagnostics.StartInboundRoute("Asterisk"))
        {
            ContactCenterDiagnostics.CompleteInboundRoute(telemetry, new InboundVoiceRoutingResult
            {
                InteractionId = "interaction-1",
                Queued = true,
                ReasonCode = "A free-text reason, +1 555 0100",
            });
        }

        // Assert
        var routed = Assert.Single(capture.Find("crestapps.contactcenter.inbound.routed"));
        Assert.Equal("queued", routed.Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
        Assert.Equal(CrestAppsOrchardCoreTelemetry.Other, routed.Tags[CrestAppsOrchardCoreTelemetry.ReasonTag]);

        var duration = Assert.Single(capture.Find("crestapps.contactcenter.inbound.route.duration"));
        Assert.Equal(CrestAppsOrchardCoreTelemetry.OutcomeOk, duration.Tags[CrestAppsOrchardCoreTelemetry.OutcomeTag]);
        Assert.Equal("Asterisk", duration.Tags[CrestAppsOrchardCoreTelemetry.ProviderTag]);

        var span = Assert.Single(capture.Spans, activity => activity.OperationName == "crestapps.contactcenter.inbound.route");
        Assert.Equal("interaction-1", span.GetTagItem("crestapps.contactcenter.interaction.id"));
        Assert.Empty(capture.FindBannedTagKeys());
    }

    [Fact]
    public void InboundRoute_ACallAlreadyTracked_IsCountedAsADuplicateNotRoutedAgain()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();

        // Act
        using (var telemetry = ContactCenterDiagnostics.StartInboundRoute("Telnyx"))
        {
            ContactCenterDiagnostics.CompleteInboundRoute(telemetry, new InboundVoiceRoutingResult { Routed = true }, duplicate: true);
        }

        // Assert
        Assert.Equal("duplicate", Assert.Single(capture.Find("crestapps.contactcenter.inbound.routed")).Tags[CrestAppsOrchardCoreTelemetry.ResultTag]);
    }

    [Theory]
    [InlineData("ai_agent", "ai_agent")]
    [InlineData("queue_full_voicemail", "queue_full_voicemail")]
    [InlineData(null, "none")]
    [InlineData("Queue Full", "other")]
    [InlineData("+15550100", "other")]
    public void GetReasonCode_KeepsOnlyShortSnakeCaseCodes(string reasonCode, string expected)
    {
        Assert.Equal(expected, ContactCenterDiagnostics.GetReasonCode(reasonCode));
    }

    [Theory]
    [InlineData("offer:reservation-1", "offer")]
    [InlineData("queue-wait:abc", "queue_wait")]
    [InlineData("caller@example.com", "other")]
    public void GetDeadlineKind_TagsTheKeyPrefixOnly(string key, string expected)
    {
        Assert.Equal(expected, ContactCenterDiagnostics.GetDeadlineKind(key));
    }

    [Fact]
    public void Gauges_ReportTheLastObservedValue_WithoutQueryingAStore()
    {
        // Arrange
        using var capture = new TraceScopedTelemetryCapture();

        // Act
        ContactCenterDiagnostics.ObserveNodeServing(true);
        capture.RecordObservableInstruments();

        // Assert
        var serving = capture.Find("crestapps.contactcenter.node.serving");
        Assert.NotEmpty(serving);
        Assert.All(serving, measurement => Assert.InRange(measurement.Value, 0, 1));
        Assert.Empty(capture.FindBannedTagKeys());
    }
}
