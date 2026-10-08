using System.Collections.Frozen;
using CrestApps.OrchardCore.ContactCenter.Core.Models;
using CrestApps.OrchardCore.ContactCenter.Models;
using CrestApps.OrchardCore.Omnichannel.Core.Models;
using CrestApps.OrchardCore.Telephony.Models;

namespace CrestApps.OrchardCore.Tests.Modules.ContactCenter.Lifecycles;

/// <summary>
/// Walks every ordered pair of statuses for each aggregate lifecycle and compares the table's answer to an
/// expectation written out independently here.
/// <para>
/// A lifecycle table is only worth having if it is right, and the failure mode of a hand-maintained edge table is
/// a missing or extra entry that nothing notices: a missing edge blocks a flow that has to work, and an extra one
/// re-admits the illegal transition the table was added to refuse. Spot-checking a handful of transitions cannot
/// find either. These tests therefore enumerate the complete N x N matrix, so a wrong entry is a failure rather
/// than a gap in coverage.
/// </para>
/// <para>
/// The expectation below is a transliteration of the table, written in a different shape so that a one-sided
/// edit is visible. That makes the matrix tests a drift alarm rather than a proof of correctness: they catch an
/// entry changed in one place and not the other, but they cannot catch an edge that is wrong in both. The claims
/// that do not depend on the transliteration are the property tests underneath, which are derived from the
/// enums and from the settled predicates rather than from the edge lists, plus the named refusals that state the
/// specific readings the tables exist to reject.
/// </para>
/// </summary>
public sealed class AggregateLifecycleTableTests
{
    private static readonly FrozenSet<string> _interactionEdges = FrozenSet.ToFrozenSet(
    [
        "Created->Ringing", "Created->Connected", "Created->Transferring", "Created->Ended", "Created->Failed",
        "Ringing->Created", "Ringing->Connected", "Ringing->Transferring", "Ringing->Ended", "Ringing->Failed",
        "Connected->Created", "Connected->Ringing", "Connected->Held", "Connected->Transferring", "Connected->Conferenced", "Connected->Ended", "Connected->Failed",
        "Held->Created", "Held->Ringing", "Held->Connected", "Held->Transferring", "Held->Conferenced", "Held->Ended", "Held->Failed",
        "Transferring->Created", "Transferring->Ringing", "Transferring->Connected", "Transferring->Held", "Transferring->Conferenced", "Transferring->Ended", "Transferring->Failed",
        "Conferenced->Created", "Conferenced->Ringing", "Conferenced->Connected", "Conferenced->Held", "Conferenced->Transferring", "Conferenced->Ended", "Conferenced->Failed",
    ]);

    private static readonly FrozenSet<string> _callSessionEdges = FrozenSet.ToFrozenSet(
    [
        "Planned->Dialing", "Planned->Ringing", "Planned->Connected", "Planned->Ended", "Planned->Canceled", "Planned->Failed",
        "Dialing->Ringing", "Dialing->Connected", "Dialing->Ending", "Dialing->Ended", "Dialing->NoAnswer", "Dialing->Rejected", "Dialing->Canceled", "Dialing->Failed",
        "Ringing->Connected", "Ringing->Ending", "Ringing->Ended", "Ringing->NoAnswer", "Ringing->Rejected", "Ringing->Canceled", "Ringing->Failed",
        "Connected->Ringing", "Connected->OnHold", "Connected->Ending", "Connected->Ended", "Connected->Transferred", "Connected->Failed",
        "OnHold->Ringing", "OnHold->Connected", "OnHold->Ending", "OnHold->Ended", "OnHold->Transferred", "OnHold->Failed",
        "Ending->Ended", "Ending->Transferred", "Ending->Failed",
    ]);

    private static readonly FrozenSet<string> _queueItemEdges = FrozenSet.ToFrozenSet(
    [
        "Waiting->Reserved", "Waiting->Assigned", "Waiting->Completed", "Waiting->Removed",
        "Reserved->Waiting", "Reserved->Assigned", "Reserved->Completed", "Reserved->Removed",
        "Assigned->Waiting", "Assigned->Completed", "Assigned->Removed",
    ]);

    private static readonly FrozenSet<string> _reservationEdges = FrozenSet.ToFrozenSet(
    [
        "Pending->Accepted", "Pending->Rejected", "Pending->Expired", "Pending->Canceled",
        "Accepted->Canceled",
    ]);

    private static readonly FrozenSet<string> _workAssignmentEdges = FrozenSet.ToFrozenSet(
    [
        "Unassigned->Available", "Unassigned->Reserved", "Unassigned->Assigned", "Unassigned->Released",
        "Available->Unassigned", "Available->Reserved", "Available->Assigned", "Available->Released",
        "Reserved->Unassigned", "Reserved->Available", "Reserved->Assigned", "Reserved->Released",
        "Assigned->Unassigned", "Assigned->Available", "Assigned->InProgress", "Assigned->Released",
        "InProgress->Unassigned", "InProgress->Available", "InProgress->Released",
        "Released->Unassigned", "Released->Available", "Released->Reserved", "Released->Assigned",
    ]);

    public static TheoryData<string> SettledInteractionStatuses()
        => [nameof(InteractionStatus.Ended), nameof(InteractionStatus.Failed)];

    [Theory]
    [MemberData(nameof(SettledInteractionStatuses))]
    public void InteractionLifecycle_ForEverySettledStatus_AdmitsNoOutgoingEdge(string settled)
    {
        // Arrange
        var from = Enum.Parse<InteractionStatus>(settled);

        // Act
        var reachable = Enum.GetValues<InteractionStatus>()
            .Where(to => to != from && InteractionLifecycle.CanTransition(from, to));

        // Assert
        // This is the claim the backwards requeue and re-offer edges have to keep true. Those edges let an
        // unsettled interaction become offerable again, and the only thing stopping them from also handing out a
        // conversation that already ended is that no settled status has anywhere to go.
        Assert.Empty(reachable);
    }

    [Fact]
    public void InteractionLifecycle_ForEveryUnsettledStatus_CanStillReachAnEnding()
    {
        // Arrange
        var stranded = new List<InteractionStatus>();

        // Act
        foreach (var from in Enum.GetValues<InteractionStatus>().Where(status => !InteractionLifecycle.IsSettled(status)))
        {
            if (!InteractionLifecycle.CanTransition(from, InteractionStatus.Ended)
                && !InteractionLifecycle.CanTransition(from, InteractionStatus.Failed))
            {
                stranded.Add(from);
            }
        }

        // Assert
        // A status with no way to finish would leave the interaction, its offer and the agent holding it alive
        // forever, which is the failure a table of refusals makes easy to introduce by omission.
        Assert.Empty(stranded);
    }

    [Fact]
    public void CallSessionLifecycle_ForEveryTerminalState_AdmitsNoOutgoingEdge()
    {
        // Arrange
        var violations = new List<string>();

        // Act
        foreach (var from in Enum.GetValues<VoiceCallState>().Where(CallSessionLifecycle.IsTerminal))
        {
            foreach (var to in Enum.GetValues<VoiceCallState>().Where(state => state != from))
            {
                if (CallSessionLifecycle.CanTransition(from, to))
                {
                    violations.Add($"{from}->{to}");
                }
            }
        }

        // Assert
        Assert.Empty(violations);
    }

    [Fact]
    public void CallSessionLifecycle_FromPlanned_RefusesTheEstablishedStatesThatImplyAnAnswer()
    {
        // Assert
        // A call that was never answered cannot be on hold or transferred. This is the specific reading the table
        // was added to reject, and it is stated here rather than left to the matrix so that widening planned for
        // an unrelated reason cannot quietly re-admit it.
        Assert.False(CallSessionLifecycle.CanTransition(VoiceCallState.Planned, VoiceCallState.OnHold));
        Assert.False(CallSessionLifecycle.CanTransition(VoiceCallState.Planned, VoiceCallState.Transferred));
        Assert.False(CallSessionLifecycle.CanTransition(VoiceCallState.Planned, VoiceCallState.NoAnswer));
        Assert.False(CallSessionLifecycle.CanTransition(VoiceCallState.Planned, VoiceCallState.Rejected));
    }

    [Fact]
    public void InteractionLifecycle_OverTheCompleteMatrix_AdmitsExactlyTheDeclaredEdges()
        => AssertMatrix<InteractionStatus>(_interactionEdges, InteractionLifecycle.CanTransition);

    [Fact]
    public void CallSessionLifecycle_OverTheCompleteMatrix_AdmitsExactlyTheDeclaredEdges()
        => AssertMatrix<VoiceCallState>(_callSessionEdges, CallSessionLifecycle.CanTransition);

    [Fact]
    public void QueueItemLifecycle_OverTheCompleteMatrix_AdmitsExactlyTheDeclaredEdges()
        => AssertMatrix<QueueItemStatus>(_queueItemEdges, QueueItemLifecycle.CanTransition);

    [Fact]
    public void ReservationLifecycle_OverTheCompleteMatrix_AdmitsExactlyTheDeclaredEdges()
        => AssertMatrix<ReservationStatus>(_reservationEdges, ReservationLifecycle.CanTransition);

    [Fact]
    public void WorkAssignmentLifecycle_OverTheCompleteMatrix_AdmitsExactlyTheDeclaredEdges()
        => AssertMatrix<ActivityAssignmentStatus>(_workAssignmentEdges, WorkAssignmentLifecycle.CanTransition);

    [Fact]
    public void EveryLifecycle_ForEveryStatus_AdmitsTheStatusItAlreadyHolds()
    {
        // Providers redeliver, and a redelivery that changes nothing is not a transition. If this stopped being
        // true, at-least-once delivery would start producing errors on correct input.
        foreach (var status in Enum.GetValues<InteractionStatus>())
        {
            Assert.True(InteractionLifecycle.CanTransition(status, status), $"Interaction {status}");
        }

        foreach (var state in Enum.GetValues<VoiceCallState>())
        {
            Assert.True(CallSessionLifecycle.CanTransition(state, state), $"CallSession {state}");
        }

        foreach (var status in Enum.GetValues<QueueItemStatus>())
        {
            Assert.True(QueueItemLifecycle.CanTransition(status, status), $"QueueItem {status}");
        }

        foreach (var status in Enum.GetValues<ReservationStatus>())
        {
            Assert.True(ReservationLifecycle.CanTransition(status, status), $"Reservation {status}");
        }

        foreach (var status in Enum.GetValues<ActivityAssignmentStatus>())
        {
            Assert.True(WorkAssignmentLifecycle.CanTransition(status, status), $"WorkAssignment {status}");
        }
    }

    [Fact]
    public void SettledStatuses_ForEveryLifecycleThatHasThem_HaveNoOutgoingEdge()
    {
        // The whole reason these lifecycles exist is that a settled record must stay settled: an ended call that
        // starts ringing again is two calls merged into one history, and a completed queue item that re-enters
        // the queue is work handed to a second agent after the first already did it.
        AssertNoWayOut(Enum.GetValues<InteractionStatus>(), InteractionLifecycle.IsSettled, InteractionLifecycle.CanTransition);
        AssertNoWayOut(Enum.GetValues<VoiceCallState>(), CallSessionLifecycle.IsTerminal, CallSessionLifecycle.CanTransition);
        AssertNoWayOut(Enum.GetValues<QueueItemStatus>(), QueueItemLifecycle.IsSettled, QueueItemLifecycle.CanTransition);

        // A reservation is a special case: Accepted is resolved in the sense that the lock is no longer held, but
        // it is not final, because the assignment it produced can still be released. Only the outcomes that end
        // the reservation for good are checked for a way out.
        AssertNoWayOut(
            Enum.GetValues<ReservationStatus>(),
            status => status is ReservationStatus.Rejected or ReservationStatus.Expired or ReservationStatus.Canceled,
            ReservationLifecycle.CanTransition);
    }

    [Fact]
    public void EveryLifecycle_ForEveryDeclaredStatus_IsReachableFromTheStartingStatus()
    {
        // A status the table can never reach is either a dead enum member or a missing edge, and both are
        // defects that would otherwise sit unnoticed until a provider produced the state in production.
        AssertAllReachable(InteractionStatus.Created, Enum.GetValues<InteractionStatus>(), InteractionLifecycle.CanTransition);
        AssertAllReachable(VoiceCallState.Planned, Enum.GetValues<VoiceCallState>(), CallSessionLifecycle.CanTransition);
        AssertAllReachable(QueueItemStatus.Waiting, Enum.GetValues<QueueItemStatus>(), QueueItemLifecycle.CanTransition);
        AssertAllReachable(ReservationStatus.Pending, Enum.GetValues<ReservationStatus>(), ReservationLifecycle.CanTransition);
        AssertAllReachable(ActivityAssignmentStatus.Unassigned, Enum.GetValues<ActivityAssignmentStatus>(), WorkAssignmentLifecycle.CanTransition);
    }

    private static void AssertMatrix<TStatus>(FrozenSet<string> expectedEdges, Func<TStatus, TStatus, bool> canTransition)
        where TStatus : struct, Enum
    {
        var unexpected = new List<string>();
        var missing = new List<string>();

        foreach (var from in Enum.GetValues<TStatus>())
        {
            foreach (var to in Enum.GetValues<TStatus>())
            {
                if (from.Equals(to))
                {
                    continue;
                }

                var edge = $"{from}->{to}";
                var admitted = canTransition(from, to);
                var expected = expectedEdges.Contains(edge);

                if (admitted && !expected)
                {
                    unexpected.Add(edge);
                }
                else if (!admitted && expected)
                {
                    missing.Add(edge);
                }
            }
        }

        Assert.True(
            unexpected.Count == 0 && missing.Count == 0,
            $"{typeof(TStatus).Name} lifecycle disagrees with the declared edges. " +
            $"Admitted but not declared: [{string.Join(", ", unexpected)}]. " +
            $"Declared but refused: [{string.Join(", ", missing)}].");
    }

    private static void AssertNoWayOut<TStatus>(
        TStatus[] all,
        Func<TStatus, bool> isSettled,
        Func<TStatus, TStatus, bool> canTransition)
        where TStatus : struct, Enum
    {
        foreach (var from in all.Where(isSettled))
        {
            foreach (var to in all.Where(x => !x.Equals(from)))
            {
                Assert.False(
                    canTransition(from, to),
                    $"{typeof(TStatus).Name} '{from}' is settled but can still move to '{to}'.");
            }
        }
    }

    private static void AssertAllReachable<TStatus>(
        TStatus start,
        TStatus[] all,
        Func<TStatus, TStatus, bool> canTransition)
        where TStatus : struct, Enum
    {
        var reached = new HashSet<TStatus> { start };
        var frontier = new Queue<TStatus>();
        frontier.Enqueue(start);

        while (frontier.Count > 0)
        {
            var current = frontier.Dequeue();

            foreach (var next in all)
            {
                if (!reached.Contains(next) && canTransition(current, next))
                {
                    reached.Add(next);
                    frontier.Enqueue(next);
                }
            }
        }

        var unreachable = all.Where(x => !reached.Contains(x)).ToList();

        Assert.True(
            unreachable.Count == 0,
            $"{typeof(TStatus).Name} statuses are declared but unreachable from '{start}': [{string.Join(", ", unreachable)}].");
    }
}
