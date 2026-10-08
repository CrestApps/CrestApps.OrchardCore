using CrestApps.OrchardCore.ContactCenter;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Core.Services;
using CrestApps.OrchardCore.ContactCenter.Models;
using static CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports.AuditEvents;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Reports;

public sealed class InteractionOutcomeClassifierTests
{
    private static readonly DateTime _start = new(2026, 9, 24, 21, 15, 33, DateTimeKind.Utc);

    [Fact]
    public void Classify_ACallerWhoHungUpWhileTheQueueWasPlayingToThem_AbandonedAlthoughThePlatformAnswered()
    {
        // Arrange: the platform answered the caller to play the queue, which the provider reports as an answer.
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 1, endedAfter: 45);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start),
            Call(ContactCenterConstants.Events.CallAbandoned, "call-1", _start.AddSeconds(45), durationSeconds: 45),
        ]);

        // Act
        var outcome = outcomes.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.Abandoned, outcome);
        Assert.Equal(45, outcomes.GetWaitBeforeAbandonSeconds(interaction), 6);
    }

    [Fact]
    public void Classify_ACallAnAgentAnsweredAndTheCallerThenHungUp_Answered()
    {
        // Arrange
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 12, endedAfter: 300);

        // Act
        var outcome = InteractionOutcomeClassifier.WithoutEvents.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.Answered, outcome);
    }

    [Fact]
    public void Classify_ACallFlaggedAsSentToVoicemail_VoicemailEvenWithoutItsEvent()
    {
        // Arrange: the event written when the call was sent can be lost; the flag on the interaction is not.
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 74, endedAfter: 106);
        interaction.TechnicalMetadata[ContactCenterConstants.Voicemail.ProjectionMetadataKey] = "true";

        // Act
        var outcome = InteractionOutcomeClassifier.WithoutEvents.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.Voicemail, outcome);
    }

    [Fact]
    public void GetWaitBeforeVoicemailSeconds_WithoutTheQueueDeparture_MeasuresToTheVoicemailNotToTheEndOfTheMessage()
    {
        // Arrange
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: null, endedAfter: 140);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start.AddSeconds(2)),
            Call(ContactCenterConstants.Events.CallSentToVoicemail, "call-1", _start.AddSeconds(62)),
        ]);

        // Act
        var wait = outcomes.GetWaitBeforeVoicemailSeconds(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.Voicemail, outcomes.Classify(interaction));
        Assert.Equal(60, wait, 6);
    }

    [Theory]
    [InlineData(InteractionDirection.Inbound, InteractionStatus.Failed, InteractionOutcome.Failed)]
    [InlineData(InteractionDirection.Outbound, InteractionStatus.Failed, InteractionOutcome.Failed)]
    [InlineData(InteractionDirection.Inbound, InteractionStatus.Ended, InteractionOutcome.Abandoned)]
    [InlineData(InteractionDirection.Outbound, InteractionStatus.Ended, InteractionOutcome.NotConnected)]
    [InlineData(InteractionDirection.Inbound, InteractionStatus.Ringing, InteractionOutcome.InProgress)]
    public void Classify_AnUnansweredCallWithNothingOnRecord_FallsBackToHowItsSessionSettled(
        InteractionDirection direction,
        InteractionStatus status,
        InteractionOutcome expected)
    {
        // Arrange
        var interaction = new Interaction
        {
            ItemId = "call-1",
            Direction = direction,
            CreatedUtc = _start,
        }.RestorePersistedStatus(status);

        // Act
        var outcome = InteractionOutcomeClassifier.WithoutEvents.Classify(interaction);

        // Assert
        Assert.Equal(expected, outcome);
    }

    [Fact]
    public void Classify_AStoredFailureWithAnAbandonOnRecord_Abandoned()
    {
        // Arrange: before the provider's "cancelled" stopped meaning failed, a caller who hung up while an agent was
        // being offered the call settled as failed. The abandon the routing engine recorded says what happened.
        var interaction = Inbound("call-1", InteractionStatus.Failed, answeredAfter: null, endedAfter: 41);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallAbandoned, "call-1", _start.AddSeconds(41), durationSeconds: 40.96),
        ]);

        // Act
        var outcome = outcomes.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.Abandoned, outcome);
        Assert.Equal(40.96, outcomes.GetWaitBeforeAbandonSeconds(interaction), 6);
    }

    [Fact]
    public void GetTalkAndWaitSeconds_ACallSentToVoicemail_WaitedUntilItLeftTheQueue_AndTalkedToNobody()
    {
        // Arrange: queued at once, sent to voicemail after 30s, the platform answered to record and the caller hung up.
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 27.7, endedAfter: 33.2);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start),
            Call(ContactCenterConstants.Events.CallDequeued, "call-1", _start.AddSeconds(30)),
            Call(ContactCenterConstants.Events.CallSentToVoicemail, "call-1", _start.AddSeconds(30.2)),
        ]);

        // Act
        var talk = outcomes.GetTalkSeconds(interaction);
        var wait = outcomes.GetWaitSeconds(interaction);

        // Assert
        Assert.Equal(0d, talk);
        Assert.Equal(30, wait, 6);
    }

    [Fact]
    public void GetTalkAndWaitSeconds_ACallerWhoAbandonedWhileTheQueuePlayed_WaitedUntilHangingUp_AndTalkedToNobody()
    {
        // Arrange
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 1, endedAfter: 45);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start),
            Call(ContactCenterConstants.Events.CallAbandoned, "call-1", _start.AddSeconds(45), durationSeconds: 45),
        ]);

        // Act & Assert
        Assert.Equal(0d, outcomes.GetTalkSeconds(interaction));
        Assert.Equal(45, outcomes.GetWaitSeconds(interaction), 6);
    }

    [Fact]
    public void GetTalkAndWaitSeconds_AnAnsweredCall_WaitedForTheAnswer_AndTalkedUntilItEnded()
    {
        // Arrange
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 12, endedAfter: 300);

        // Act & Assert
        Assert.Equal(288, InteractionOutcomeClassifier.WithoutEvents.GetTalkSeconds(interaction), 6);
        Assert.Equal(12, InteractionOutcomeClassifier.WithoutEvents.GetWaitSeconds(interaction), 6);
    }

    [Fact]
    public void Classify_ACallerWhoTookACallbackFromTheQueue_CallbackRequestedNotAbandonedNorAnswered()
    {
        // Arrange: the platform answered them to play the queue, and the call ended once the callback was confirmed.
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: 1, endedAfter: 52);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start.AddSeconds(2)),
            Call(ContactCenterConstants.Events.CallDequeued, "call-1", _start.AddSeconds(47), durationSeconds: 45),
            Call(ContactCenterConstants.Events.CallbackRequested, "call-1", _start.AddSeconds(47), durationSeconds: 45),
        ]);

        // Act
        var outcome = outcomes.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.CallbackRequested, outcome);
        Assert.False(outcomes.IsAbandoned(interaction));
        Assert.False(outcomes.IsAnswered(interaction));
        Assert.True(outcomes.IsCallbackRequested(interaction));
        Assert.Equal(45, outcomes.GetWaitSeconds(interaction), 6);
        Assert.Equal(0d, outcomes.GetTalkSeconds(interaction));
    }

    [Fact]
    public void Classify_ACallFlaggedAsTakingACallback_CallbackRequestedEvenWithoutItsEvent()
    {
        // Arrange: nothing answered it and nothing recorded an abandon, which alone reads as an abandon.
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: null, endedAfter: 40);
        interaction.TechnicalMetadata[QueueCallbackOfferResponder.RoutingTerminalReasonMetadataKey] = QueueCallbackOfferResponder.ReasonCode;

        // Act
        var outcome = InteractionOutcomeClassifier.WithoutEvents.Classify(interaction);

        // Assert
        Assert.Equal(InteractionOutcome.CallbackRequested, outcome);
    }

    [Fact]
    public void GetWaitBeforeCallbackSeconds_WithoutTheEventsWait_MeasuresFromJoiningTheQueueToAccepting()
    {
        // Arrange
        var interaction = Inbound("call-1", InteractionStatus.Ended, answeredAfter: null, endedAfter: 70);
        var outcomes = InteractionOutcomeClassifier.FromEvents(
        [
            Call(ContactCenterConstants.Events.CallQueued, "call-1", _start.AddSeconds(5)),
            Call(ContactCenterConstants.Events.CallbackRequested, "call-1", _start.AddSeconds(65)),
        ]);

        // Act
        var wait = outcomes.GetWaitBeforeCallbackSeconds(interaction);

        // Assert
        Assert.Equal(60, wait, 6);
    }

    private static Interaction Inbound(string id, InteractionStatus status, double? answeredAfter, double? endedAfter)
        => new Interaction
        {
            ItemId = id,
            Channel = InteractionChannel.Voice,
            Direction = InteractionDirection.Inbound,
            CreatedUtc = _start,
            AnsweredUtc = answeredAfter.HasValue ? _start.AddSeconds(answeredAfter.Value) : null,
            EndedUtc = endedAfter.HasValue ? _start.AddSeconds(endedAfter.Value) : null,
        }.RestorePersistedStatus(status);
}
