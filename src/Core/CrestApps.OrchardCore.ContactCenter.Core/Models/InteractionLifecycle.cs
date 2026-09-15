using System.Collections.Frozen;
using CrestApps.OrchardCore.ContactCenter.Models;

namespace CrestApps.OrchardCore.ContactCenter.Core.Models;

/// <summary>
/// Declares which interaction status changes the domain admits.
/// <para>
/// Provider streams were previously ordered by a lifecycle rank, which only answers whether a change moves the
/// call forward. Forward is not the same as legal: an interaction that was created and never rang can move to
/// <see cref="InteractionStatus.Held"/> without regressing anything, and a held call that was never answered is
/// a fiction that every report, wallboard and duration calculation downstream will treat as real. The table
/// below is the one place that says which edges exist, so the answer cannot differ between the ingestion path,
/// the agent workspace and the reporting projection.
/// </para>
/// </summary>
public static class InteractionLifecycle
{
    private static readonly FrozenDictionary<InteractionStatus, FrozenSet<InteractionStatus>> _transitions =
        new Dictionary<InteractionStatus, FrozenSet<InteractionStatus>>
        {
            // A provider that answers immediately reports Connected without an intermediate alert, and a call
            // that is abandoned before it is offered anywhere settles straight from Created. Transferring is
            // admitted from Created because a queued interaction that has not been offered to anyone can still
            // be moved to a different queue, which is a transfer of the work rather than of a live conversation.
            [InteractionStatus.Created] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Ringing,
                InteractionStatus.Connected,
                InteractionStatus.Transferring,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),
            // Created and Ringing are reachable again from every unsettled status because an offer that is not
            // taken has to become offerable again: a reservation that expires returns the interaction to routing,
            // and a re-offer alerts the next agent. Neither edge leaves a settled status, so a conversation that
            // is over still cannot be handed to anyone.
            [InteractionStatus.Ringing] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Created,
                InteractionStatus.Connected,
                InteractionStatus.Transferring,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),
            [InteractionStatus.Connected] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Created,
                InteractionStatus.Ringing,
                InteractionStatus.Held,
                InteractionStatus.Transferring,
                InteractionStatus.Conferenced,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),
            [InteractionStatus.Held] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Created,
                InteractionStatus.Ringing,
                InteractionStatus.Connected,
                InteractionStatus.Transferring,
                InteractionStatus.Conferenced,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),
            [InteractionStatus.Transferring] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Created,
                InteractionStatus.Ringing,
                InteractionStatus.Connected,
                InteractionStatus.Held,
                InteractionStatus.Conferenced,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),
            [InteractionStatus.Conferenced] = FrozenSet.ToFrozenSet(
            [
                InteractionStatus.Created,
                InteractionStatus.Ringing,
                InteractionStatus.Connected,
                InteractionStatus.Held,
                InteractionStatus.Transferring,
                InteractionStatus.Ended,
                InteractionStatus.Failed,
            ]),

            // Settled statuses are final. A provider that redelivers a hangup after the call is already ended
            // is answered by the same-status rule, not by an edge out of a settled state.
            [InteractionStatus.Ended] = FrozenSet<InteractionStatus>.Empty,
            [InteractionStatus.Failed] = FrozenSet<InteractionStatus>.Empty,
        }.ToFrozenDictionary();

    /// <summary>
    /// Determines whether an interaction in one status may move to another.
    /// </summary>
    /// <param name="from">The status the interaction is in.</param>
    /// <param name="to">The status the interaction would move to.</param>
    /// <returns><see langword="true"/> when the transition is admitted; otherwise <see langword="false"/>.</returns>
    public static bool CanTransition(InteractionStatus from, InteractionStatus to)
    {
        // Re-applying the status an interaction already holds is not a transition. Provider streams redeliver,
        // and refusing a redelivery that changes nothing would turn an at-least-once provider into an error.
        if (from == to)
        {
            return true;
        }

        return _transitions.TryGetValue(from, out var allowed) && allowed.Contains(to);
    }

    /// <summary>
    /// Determines whether an interaction status is settled, meaning its communication session has reached an
    /// outcome and no further transition can move it.
    /// </summary>
    /// <param name="status">The status to inspect.</param>
    /// <returns><see langword="true"/> when the status is settled; otherwise <see langword="false"/>.</returns>
    public static bool IsSettled(InteractionStatus status)
        => status == InteractionStatus.Ended || status == InteractionStatus.Failed;
}
