using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Lifecycles;

/// <summary>
/// Covers the behaviour the aggregates gained when their status setters were closed: an illegal move is refused
/// rather than recorded, and the operations that used to be spelled out at every call site now mean one thing.
/// </summary>
public sealed class AggregateStateTransitionTests
{
    [Fact]
    public void Interaction_TransitionTo_WhenTheEdgeDoesNotExist_ThrowsAndLeavesTheStatusUnchanged()
    {
        // Arrange
        var interaction = new Interaction();

        // Act
        var exception = Assert.Throws<InvalidStateTransitionException>(
            () => interaction.TransitionTo(InteractionStatus.Held));

        // Assert
        Assert.Equal(nameof(Interaction), exception.AggregateName);
        Assert.Equal(InteractionStatus.Created, exception.From);
        Assert.Equal(InteractionStatus.Held, exception.To);
        Assert.Equal(InteractionStatus.Created, interaction.Status);
    }

    [Fact]
    public void Interaction_TransitionTo_WhenTheInteractionHasSettled_RefusesEveryFurtherMove()
    {
        var interaction = new Interaction();
        interaction.TransitionTo(InteractionStatus.Ringing);
        interaction.TransitionTo(InteractionStatus.Connected);
        interaction.TransitionTo(InteractionStatus.Ended);

        Assert.True(interaction.IsSettled);
        Assert.Throws<InvalidStateTransitionException>(() => interaction.TransitionTo(InteractionStatus.Connected));
        Assert.Throws<InvalidStateTransitionException>(() => interaction.TransitionTo(InteractionStatus.Ringing));
        Assert.Equal(InteractionStatus.Ended, interaction.Status);
    }

    [Fact]
    public void Interaction_Requeue_FromAnUnsettledStatus_ReturnsItToRoutingAndDropsTheAgent()
    {
        // Arrange
        var interaction = new Interaction { AgentId = "agent-1" };
        interaction.TransitionTo(InteractionStatus.Ringing);

        // Act
        interaction.Requeue();

        // Assert
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
    }

    [Fact]
    public void Interaction_Requeue_AfterTheSessionSettled_IsRefused()
    {
        // Re-offering a call that is over creates work for a conversation nobody can join.
        var interaction = new Interaction { AgentId = "agent-1" };
        interaction.TransitionTo(InteractionStatus.Failed);

        Assert.Throws<InvalidStateTransitionException>(interaction.Requeue);
        Assert.Equal(InteractionStatus.Failed, interaction.Status);
        Assert.Equal("agent-1", interaction.AgentId);
    }

    [Fact]
    public void Interaction_Reoffer_FromAConnectedCall_IsAdmittedButRefusedOnceSettled()
    {
        var live = new Interaction();
        live.TransitionTo(InteractionStatus.Connected);
        live.Reoffer();

        Assert.Equal(InteractionStatus.Ringing, live.Status);

        var settled = new Interaction();
        settled.TransitionTo(InteractionStatus.Ended);

        Assert.Throws<InvalidStateTransitionException>(settled.Reoffer);
    }

    [Fact]
    public void Interaction_MirrorSessionStatus_AppliesTheStatusTheSessionImplies_WithoutConsultingTheTable()
    {
        // The call session is the authority for a provider-backed call. Refusing here would let the two records
        // disagree, which is the divergence CallStateMachinePropertyTests exists to catch.
        var interaction = new Interaction();

        Assert.False(interaction.CanTransitionTo(InteractionStatus.Held));

        interaction.MirrorSessionStatus(InteractionStatus.Held);

        Assert.Equal(InteractionStatus.Held, interaction.Status);
    }

    [Fact]
    public void CallSession_TransitionTo_WhenTheCallHasReachedAnOutcome_RefusesToRestartIt()
    {
        var session = new CallSession();
        session.TransitionTo(VoiceCallState.Ringing);
        session.TransitionTo(VoiceCallState.Connected);
        session.TransitionTo(VoiceCallState.Ended);

        Assert.True(session.IsTerminal);

        var exception = Assert.Throws<InvalidStateTransitionException>(
            () => session.TransitionTo(VoiceCallState.Ringing));

        Assert.Equal(nameof(CallSession), exception.AggregateName);
        Assert.Equal(VoiceCallState.Ended, session.State);
    }

    [Fact]
    public void CallSession_TransitionTo_FromPlannedStraightToHeld_IsRefused()
    {
        // Phase ordering cannot see this: planned and held are different phases and the move is forward, so the
        // pre-existing ordering guard would let a call be recorded as held that was never answered.
        var session = new CallSession();

        Assert.Throws<InvalidStateTransitionException>(() => session.TransitionTo(VoiceCallState.OnHold));
    }

    [Fact]
    public void QueueItem_TransitionTo_AfterCompletion_RefusesToReturnTheItemToTheQueue()
    {
        var item = new QueueItem();
        item.TransitionTo(QueueItemStatus.Reserved);
        item.TransitionTo(QueueItemStatus.Assigned);
        item.TransitionTo(QueueItemStatus.Completed);

        Assert.True(item.IsSettled);
        Assert.Throws<InvalidStateTransitionException>(() => item.TransitionTo(QueueItemStatus.Waiting));
    }

    [Fact]
    public void QueueItem_TransitionTo_FromReservedBackToWaiting_IsAdmitted()
    {
        // A reservation that expires has to return the item to the queue for the next agent.
        var item = new QueueItem();
        item.TransitionTo(QueueItemStatus.Reserved);
        item.TransitionTo(QueueItemStatus.Waiting);

        Assert.Equal(QueueItemStatus.Waiting, item.Status);
    }

    [Fact]
    public void ActivityReservation_TransitionTo_AfterItResolved_RefusesASecondOutcome()
    {
        // A reservation resolving twice is what allows the same activity to be handed to two agents.
        var reservation = new ActivityReservation();
        reservation.TransitionTo(ReservationStatus.Rejected);

        Assert.True(reservation.IsResolved);
        Assert.Throws<InvalidStateTransitionException>(() => reservation.TransitionTo(ReservationStatus.Accepted));
        Assert.Throws<InvalidStateTransitionException>(() => reservation.TransitionTo(ReservationStatus.Expired));
    }

    [Fact]
    public void ActivityReservation_TransitionTo_FromAcceptedToCanceled_IsAdmitted()
    {
        var reservation = new ActivityReservation();
        reservation.TransitionTo(ReservationStatus.Accepted);
        reservation.TransitionTo(ReservationStatus.Canceled);

        Assert.Equal(ReservationStatus.Canceled, reservation.Status);
    }

    [Fact]
    public void ContactCenterWorkState_TransitionTo_FromUnassignedStraightToInProgress_IsRefused()
    {
        // Work in progress without an assignment has no agent, and routing would treat it as busy while nobody
        // is working it.
        var workState = new ContactCenterWorkState();

        Assert.Throws<InvalidStateTransitionException>(
            () => workState.TransitionTo(ActivityAssignmentStatus.InProgress));
    }

    [Fact]
    public void ContactCenterWorkState_TransitionTo_FromReleasedBackToAvailable_IsAdmitted()
    {
        // Released is not terminal: the same work is dialed again on a later cycle.
        var workState = new ContactCenterWorkState();
        workState.TransitionTo(ActivityAssignmentStatus.Assigned);
        workState.TransitionTo(ActivityAssignmentStatus.Released);
        workState.TransitionTo(ActivityAssignmentStatus.Available);

        Assert.Equal(ActivityAssignmentStatus.Available, workState.AssignmentStatus);
    }

    [Fact]
    public void ContactCenterWorkState_AdoptActivityAssignmentStatus_SeedsWithoutCheckingTheEdge()
    {
        // Seeding a work state from an activity that is already assigned is not a transition, and treating it as
        // one would refuse every activity that already carries a routing status.
        var workState = new ContactCenterWorkState();

        Assert.False(workState.CanTransitionTo(ActivityAssignmentStatus.InProgress));

        workState.AdoptActivityAssignmentStatus(ActivityAssignmentStatus.InProgress);

        Assert.Equal(ActivityAssignmentStatus.InProgress, workState.AssignmentStatus);
    }

    [Fact]
    public void EveryAggregate_ReapplyingTheStatusItAlreadyHolds_IsNotTreatedAsATransition()
    {
        // At-least-once delivery redelivers, and refusing a redelivery that changes nothing would turn correct
        // provider behaviour into an error.
        var interaction = new Interaction();
        interaction.TransitionTo(InteractionStatus.Ended);
        interaction.TransitionTo(InteractionStatus.Ended);

        var session = new CallSession();
        session.TransitionTo(VoiceCallState.Failed);
        session.TransitionTo(VoiceCallState.Failed);

        var item = new QueueItem();
        item.TransitionTo(QueueItemStatus.Removed);
        item.TransitionTo(QueueItemStatus.Removed);

        var reservation = new ActivityReservation();
        reservation.TransitionTo(ReservationStatus.Expired);
        reservation.TransitionTo(ReservationStatus.Expired);

        Assert.Equal(InteractionStatus.Ended, interaction.Status);
        Assert.Equal(VoiceCallState.Failed, session.State);
        Assert.Equal(QueueItemStatus.Removed, item.Status);
        Assert.Equal(ReservationStatus.Expired, reservation.Status);
    }

    [Theory]
    [InlineData(InteractionStatus.Ringing)]
    [InlineData(InteractionStatus.Connected)]
    [InlineData(InteractionStatus.Held)]
    [InlineData(InteractionStatus.Transferring)]
    [InlineData(InteractionStatus.Conferenced)]
    public void Interaction_Requeue_FromAnyLiveStatus_ReturnsItToRoutingAndDropsTheAgent(InteractionStatus live)
    {
        // Arrange
        var interaction = new Interaction().RestorePersistedStatus(live);
        interaction.AgentId = "agent-1";

        // Act
        interaction.Requeue();

        // Assert
        // Every status an offer can be sitting in when it expires has to be able to go back to routing, or the
        // expiry sweep would strand the work it was released from.
        Assert.Equal(InteractionStatus.Created, interaction.Status);
        Assert.Null(interaction.AgentId);
    }

    [Theory]
    [InlineData(InteractionStatus.Connected)]
    [InlineData(InteractionStatus.Held)]
    [InlineData(InteractionStatus.Transferring)]
    [InlineData(InteractionStatus.Conferenced)]
    public void Interaction_Reoffer_FromAnEstablishedStatus_AlertsTheNextAgent(InteractionStatus established)
    {
        // Arrange
        var interaction = new Interaction().RestorePersistedStatus(established);

        // Act
        interaction.Reoffer();

        // Assert
        // A queue transfer keeps the customer up while the next agent is alerted, so the live statuses have to
        // reach ringing. Before this was declared, every queue transfer that was accepted threw.
        Assert.Equal(InteractionStatus.Ringing, interaction.Status);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended)]
    [InlineData(InteractionStatus.Failed)]
    public void Interaction_Requeue_OnASettledInteraction_IsRefused(InteractionStatus settled)
    {
        // Arrange
        var interaction = new Interaction().RestorePersistedStatus(settled);

        // Act
        var exception = Assert.Throws<InvalidStateTransitionException>(interaction.Requeue);

        // Assert
        Assert.Equal(settled, exception.From);
        Assert.Equal(InteractionStatus.Created, exception.To);
    }

    [Theory]
    [InlineData(InteractionStatus.Ended)]
    [InlineData(InteractionStatus.Failed)]
    public void Interaction_MirrorSessionStatus_OnASettledInteraction_DoesNotBringItBackToLife(InteractionStatus settled)
    {
        // Arrange
        var interaction = new Interaction().RestorePersistedStatus(settled);

        // Act
        interaction.MirrorSessionStatus(InteractionStatus.Connected);

        // Assert
        // Mirroring skips the table because the provider already decided what happened, so this is the only
        // thing keeping a late provider frame from putting a finished conversation back into live work.
        Assert.Equal(settled, interaction.Status);
    }

    [Fact]
    public void Interaction_MirrorSessionStatus_OnALiveInteraction_AppliesTheProviderStatusWithoutConsultingTheTable()
    {
        // Arrange
        var interaction = new Interaction().RestorePersistedStatus(InteractionStatus.Created);

        // Act
        interaction.MirrorSessionStatus(InteractionStatus.Held);

        // Assert
        // Created->Held is not a declared edge, and mirroring still applies it: refusing here would make the
        // interaction disagree with the call session that is the authority for a provider-backed call.
        Assert.False(InteractionLifecycle.CanTransition(InteractionStatus.Created, InteractionStatus.Held));
        Assert.Equal(InteractionStatus.Held, interaction.Status);
    }

    [Fact]
    public void CallSession_MirrorProviderState_OnATerminalSession_DoesNotBringItBackToLife()
    {
        // Arrange
        var session = new CallSession().RestorePersistedState(VoiceCallState.Ended);

        // Act
        session.MirrorProviderState(VoiceCallState.Connected);

        // Assert
        Assert.Equal(VoiceCallState.Ended, session.State);
    }

    [Fact]
    public void CallSession_TransitionTo_FromConnectedBackToRinging_IsAdmittedForAQueueTransfer()
    {
        // Arrange
        var session = new CallSession().RestorePersistedState(VoiceCallState.Connected);

        // Act
        session.TransitionTo(VoiceCallState.Ringing);

        // Assert
        // One session carries both legs. The customer stays connected while the agent leg alerts the next agent.
        Assert.Equal(VoiceCallState.Ringing, session.State);
    }
}
