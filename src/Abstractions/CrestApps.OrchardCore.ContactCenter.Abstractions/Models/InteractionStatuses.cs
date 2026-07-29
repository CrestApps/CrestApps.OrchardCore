using System.Collections.Immutable;

namespace CrestApps.OrchardCore.ContactCenter.Models;

/// <summary>
/// Declares the interaction status sets the hot query paths select on. The sets are inclusive rather than
/// expressed as a chain of inequalities: an inclusive <c>IN</c> can be answered from an index that leads with the
/// status column, while a chain of <c>!=</c> tests cannot and forces a scan of every row for the agent.
/// </summary>
public static class InteractionStatuses
{
    /// <summary>
    /// The statuses in which an interaction occupies the agent handling it, and therefore counts against that
    /// agent's capacity. A created interaction is excluded because no provider session exists for it yet, and the
    /// settled statuses are excluded because the agent has been released.
    /// </summary>
    public static readonly ImmutableArray<InteractionStatus> OccupyingAgent =
    [
        InteractionStatus.Ringing,
        InteractionStatus.Connected,
        InteractionStatus.Held,
        InteractionStatus.Transferring,
        InteractionStatus.Conferenced,
    ];

    /// <summary>
    /// The statuses in which an interaction's communication session has not reached an outcome, so provider
    /// reconciliation must still ask the provider what happened to it.
    /// </summary>
    public static readonly ImmutableArray<InteractionStatus> Unsettled =
    [
        InteractionStatus.Created,
        InteractionStatus.Ringing,
        InteractionStatus.Connected,
        InteractionStatus.Held,
        InteractionStatus.Transferring,
        InteractionStatus.Conferenced,
    ];

    /// <summary>
    /// The statuses in which an interaction's communication session has reached an outcome and will not change
    /// again.
    /// </summary>
    public static readonly ImmutableArray<InteractionStatus> Settled =
    [
        InteractionStatus.Ended,
        InteractionStatus.Failed,
    ];
}
