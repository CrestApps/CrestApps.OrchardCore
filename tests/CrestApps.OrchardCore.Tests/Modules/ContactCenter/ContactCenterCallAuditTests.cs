using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.ContactCenter.Services;
using CrestApps.OrchardCore.Telephony.Models;
using CrestApps.OrchardCore.Telnyx.Endpoints;
using CrestApps.OrchardCore.Telnyx.Services;
using CrestApps.OrchardCore.Tests.Doubles;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using OrchardCore.Modules;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter;

/// <summary>
/// The pieces of the call audit that every writer shares: how an offer's ring time and a queue wait are measured,
/// how a redelivered change is told from a second one, the provider's time taking precedence over the time a
/// delivery was signed, and the two observers the modules that know nothing of the Contact Center report through.
/// </summary>
public sealed class ContactCenterCallAuditTests
{
    private static readonly DateTime _now = new(2026, 9, 23, 21, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ForOffer_ASettledOffer_RangFromWhenItWasPresented()
    {
        // Arrange
        var reservation = new ActivityReservation { ItemId = "r1", AgentId = "a1", QueueId = "q1", CreatedUtc = _now.AddSeconds(-14.5) };
        var interaction = new Interaction { ItemId = "i1", Channel = InteractionChannel.Voice };

        // Act
        var offer = ContactCenterCallAudit.ForOffer(reservation, interaction, new AgentProfile { UserId = "u1" }, _now, "Expired");

        // Assert
        Assert.Equal("i1", offer.InteractionId);
        Assert.Equal("u1", offer.UserId);
        Assert.Equal(nameof(InteractionChannel.Voice), offer.Channel);
        Assert.Equal(_now.AddSeconds(-14.5), offer.PresentedUtc);
        Assert.Equal(14.5, offer.RingSeconds);
    }

    [Fact]
    public async Task QueueChanges_ARedeliveredTransitionKeepsItsKey_ASecondVisitGetsItsOwn()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var interaction = new Interaction { ItemId = "i1", Channel = InteractionChannel.Voice };
        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-30) };

        // Act
        await recorder.RecordQueueChangeAsync(ContactCenterConstants.Events.CallDequeued, item, interaction, _now, "Assigned", cancellationToken: TestContext.Current.CancellationToken);
        await recorder.RecordQueueChangeAsync(ContactCenterConstants.Events.CallDequeued, item, interaction, _now.AddSeconds(5), "Removed", cancellationToken: TestContext.Current.CancellationToken);
        item.QueueEnteredUtc = _now.AddMinutes(1);
        await recorder.RecordQueueChangeAsync(ContactCenterConstants.Events.CallDequeued, item, interaction, _now.AddMinutes(2), "Assigned", cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(recorder.Calls[0].IdempotencyKey, recorder.Calls[1].IdempotencyKey);
        Assert.NotEqual(recorder.Calls[0].IdempotencyKey, recorder.Calls[2].IdempotencyKey);
        Assert.Equal(30, recorder.Calls[0].Data.DurationSeconds);
    }

    [Fact]
    public async Task TheRecorder_DropsARedeliveredChange_BecauseItsKeyIsAlreadyInTheLog()
    {
        // Arrange
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var stored = new List<InteractionEvent>();
        var eventStore = new Mock<IInteractionEventStore>();
        eventStore
            .Setup(store => store.ExistsByIdempotencyKeyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((string key, CancellationToken _) => keys.Contains(key));
        eventStore
            .Setup(store => store.CreateAsync(It.IsAny<InteractionEvent>(), It.IsAny<CancellationToken>()))
            .Callback<InteractionEvent, CancellationToken>((interactionEvent, _) =>
            {
                keys.Add(interactionEvent.IdempotencyKey);
                stored.Add(interactionEvent);
            })
            .Returns(ValueTask.CompletedTask);
        var clock = new Mock<IClock>();
        clock.SetupGet(value => value.UtcNow).Returns(_now);
        var publisher = new DefaultContactCenterEventPublisher(
            eventStore.Object,
            new Mock<IContactCenterOutbox>().Object,
            new Mock<IContactCenterScopeExecutor>().Object,
            clock.Object,
            NullLogger<DefaultContactCenterEventPublisher>.Instance);
        var recorder = new ContactCenterAuditRecorder(publisher, clock.Object);
        var interaction = new Interaction { ItemId = "i1", Channel = InteractionChannel.Voice };
        var item = new QueueItem { ItemId = "qi-1", QueueId = "q1", QueueEnteredUtc = _now.AddSeconds(-30) };

        // Act
        await recorder.RecordQueueChangeAsync(ContactCenterConstants.Events.CallQueued, item, interaction, item.QueueEnteredUtc, CallLifecycleReasons.Enqueued, cancellationToken: TestContext.Current.CancellationToken);
        await recorder.RecordQueueChangeAsync(ContactCenterConstants.Events.CallQueued, item, interaction, item.QueueEnteredUtc, CallLifecycleReasons.Enqueued, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        var queued = Assert.Single(stored);
        Assert.Equal("i1", queued.InteractionId);
        Assert.Equal(item.QueueEnteredUtc, queued.OccurredUtc);
        Assert.Equal(_now, queued.RecordedUtc);
    }

    [Fact]
    public void TelnyxWebhook_ThePayloadsMillisecondTime_WinsOverTheWholeSecondTheDeliveryWasSigned()
    {
        // Arrange
        var occurredUtc = new DateTime(2026, 9, 23, 21, 0, 4, 387, DateTimeKind.Utc);
        var signedUtc = new DateTime(2026, 9, 23, 21, 0, 9, DateTimeKind.Utc);

        // Act
        var withPayloadTime = TelnyxWebhookEndpoint.ResolveOccurredUtc(new TelnyxCallEvent { OccurredUtc = occurredUtc }, signedUtc);
        var withoutPayloadTime = TelnyxWebhookEndpoint.ResolveOccurredUtc(new TelnyxCallEvent(), signedUtc);

        // Assert
        Assert.Equal(occurredUtc, withPayloadTime);
        Assert.Equal(DateTimeKind.Utc, withPayloadTime.Kind);
        Assert.Equal(signedUtc, withoutPayloadTime);
    }

    [Theory]
    [InlineData(AutomatedVoiceCallObservationKind.Answered, ContactCenterConstants.Events.AiCallAnswered)]
    [InlineData(AutomatedVoiceCallObservationKind.AnswererDetected, ContactCenterConstants.Events.AiAnswererDetected)]
    [InlineData(AutomatedVoiceCallObservationKind.ConversationEnded, ContactCenterConstants.Events.AiConversationEnded)]
    public async Task AutomatedCallObserver_RecordsEachMoment_AgainstTheCallsInteraction(AutomatedVoiceCallObservationKind kind, string eventType)
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByActivityIdAsync("act-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "i1", ActivityItemId = "act-1" });
        var observer = new ContactCenterAutomatedVoiceCallObserver(interactionManager.Object, recorder);

        // Act
        await observer.ObserveAsync(new AutomatedVoiceCallObservation
        {
            Kind = kind,
            ActivityItemId = "act-1",
            ProviderName = "Telnyx",
            ProviderCallId = "call-1",
            OccurredUtc = _now,
            Answerer = "Machine",
            Outcome = "Completed",
            DispositionId = "disposition-1",
        }, TestContext.Current.CancellationToken);

        // Assert
        var call = Assert.Single(recorder.Calls);
        Assert.Equal(eventType, call.EventType);
        Assert.Equal("i1", call.Data.InteractionId);
        Assert.Equal("act-1", call.Data.ActivityItemId);
        Assert.Equal("call-1", call.Data.ProviderCallId);
        Assert.Equal("Machine", call.Data.Details["answerer"]);
        Assert.Equal("disposition-1", call.Data.Details["dispositionId"]);
        Assert.Equal(_now, call.OccurredUtc);
        Assert.Equal(ContactCenterActorType.AiAgent, call.Actor.Type);
    }

    [Fact]
    public async Task TelephonyCallObserver_RecordsAnExtensionCall_WithTheAgentOnIt()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var agentManager = new Mock<IAgentProfileManager>();
        agentManager
            .Setup(manager => manager.FindByUserIdAsync("user-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AgentProfile { ItemId = "agent-1", UserId = "user-1" });
        var observer = new ContactCenterTelephonyCallObserver(
            new Mock<IInteractionManager>().Object,
            agentManager.Object,
            new Lazy<IContactCenterAuditRecorder>(recorder),
            NullLogger<ContactCenterTelephonyCallObserver>.Instance);
        var call = new TelephonyInteraction
        {
            InteractionId = "telephony-1",
            CallId = "call-1",
            ProviderName = "Telnyx",
            UserId = "user-1",
            Direction = CallDirection.Outbound,
            To = "Front desk",
            IsExtension = true,
            ExtensionNumber = "204",
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now,
        };

        // Act
        await observer.CallStartedAsync(call, TestContext.Current.CancellationToken);
        call.Outcome = CallOutcome.Completed;
        call.EndedUtc = _now.AddSeconds(95);
        call.DurationSeconds = 95;
        await observer.CallEndedAsync(call, TestContext.Current.CancellationToken);
        await observer.CallEndedAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(
            [ContactCenterConstants.Events.ExtensionCallStarted, ContactCenterConstants.Events.ExtensionCallEnded, ContactCenterConstants.Events.ExtensionCallEnded],
            recorder.Calls.Select(record => record.EventType));
        Assert.All(recorder.Calls, record => Assert.Equal("agent-1", record.Data.AgentId));
        Assert.Equal(_now, recorder.Calls[0].OccurredUtc);
        Assert.Equal(_now.AddSeconds(95), recorder.Calls[1].OccurredUtc);
        Assert.Equal(95, recorder.Calls[1].Data.DurationSeconds);
        Assert.Equal("204", recorder.Calls[1].Data.Details["extensionNumber"]);

        // The history store reports a call each time it saves it; the key is what makes the repeat one record.
        Assert.Equal(recorder.Calls[1].IdempotencyKey, recorder.Calls[2].IdempotencyKey);
        Assert.Equal(ContactCenterActorType.Agent, recorder.Calls[0].Actor.Type);
    }

    [Fact]
    public async Task TelephonyCallObserver_SkipsACallTheContactCenterRouted()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByIdAsync("i1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "i1" });
        var observer = new ContactCenterTelephonyCallObserver(
            interactionManager.Object,
            new Mock<IAgentProfileManager>().Object,
            new Lazy<IContactCenterAuditRecorder>(recorder),
            NullLogger<ContactCenterTelephonyCallObserver>.Instance);

        // Act
        await observer.CallStartedAsync(new TelephonyInteraction { InteractionId = "i1", CallId = "call-1", UserId = "user-1", StartedUtc = _now }, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public async Task TelephonyCallObserver_SkipsTheRingOfAnOfferedCall_WhichTheHistoryRecordsUnderItsOwnIdentifier()
    {
        // Arrange
        // Ringing an offer on the soft phone records the call in the agent's history under a fresh identifier,
        // keyed by the caller's provider call id. An AI handoff answered on a pre-dialed leg was written to the
        // audit log a second time, as an extension call running for the whole conversation.
        var recorder = new RecordingContactCenterAuditRecorder();
        var interactionManager = new Mock<IInteractionManager>();
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "caller-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Interaction { ItemId = "i1", ProviderName = "Telnyx", ProviderInteractionId = "caller-1" });
        var observer = new ContactCenterTelephonyCallObserver(
            interactionManager.Object,
            new Mock<IAgentProfileManager>().Object,
            new Lazy<IContactCenterAuditRecorder>(recorder),
            NullLogger<ContactCenterTelephonyCallObserver>.Instance);
        var call = new TelephonyInteraction
        {
            InteractionId = "telephony-9",
            CallId = "caller-1",
            ProviderName = "Telnyx",
            UserId = "user-1",
            Direction = CallDirection.Inbound,
            Outcome = CallOutcome.InProgress,
            StartedUtc = _now,
        };

        // Act
        await observer.CallStartedAsync(call, TestContext.Current.CancellationToken);
        call.Outcome = CallOutcome.Completed;
        call.EndedUtc = _now.AddSeconds(55);
        call.DurationSeconds = 55;
        await observer.CallEndedAsync(call, TestContext.Current.CancellationToken);

        // Assert
        Assert.Empty(recorder.Calls);
    }

    [Fact]
    public async Task TelephonyCallObserver_StillRecordsAnExtensionCall_WhoseCallTheContactCenterDoesNotTrack()
    {
        // Arrange
        var recorder = new RecordingContactCenterAuditRecorder();
        var interactionManager = new Mock<IInteractionManager>(MockBehavior.Strict);
        interactionManager
            .Setup(manager => manager.FindByIdAsync("telephony-2", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        interactionManager
            .Setup(manager => manager.FindByProviderInteractionIdAsync("Telnyx", "extension-leg-1", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Interaction)null);
        var observer = new ContactCenterTelephonyCallObserver(
            interactionManager.Object,
            new Mock<IAgentProfileManager>().Object,
            new Lazy<IContactCenterAuditRecorder>(recorder),
            NullLogger<ContactCenterTelephonyCallObserver>.Instance);

        // Act
        await observer.CallStartedAsync(new TelephonyInteraction
        {
            InteractionId = "telephony-2",
            CallId = "extension-leg-1",
            ProviderName = "Telnyx",
            UserId = "user-1",
            Direction = CallDirection.Outbound,
            IsExtension = true,
            ExtensionNumber = "204",
            StartedUtc = _now,
        }, TestContext.Current.CancellationToken);

        // Assert
        var recorded = Assert.Single(recorder.Calls);
        Assert.Equal(ContactCenterConstants.Events.ExtensionCallStarted, recorded.EventType);
        Assert.Equal("extension-leg-1", recorded.Data.ProviderCallId);
    }
}
